using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartMosquitoControl.Data;
using SmartMosquitoControl.Models;

namespace SmartMosquitoControl.Services;

public enum QueueSprayOutcome { Queued, NoDevice, Busy }

public sealed record QueueSprayResult(QueueSprayOutcome Outcome, DeviceCommand? Command = null);

/// <summary>
/// The command channel between the web app and the physical sprayers.
/// The app queues <see cref="DeviceCommand"/> rows; a sprayer polls the device API, receives them,
/// and reports back. Nothing here depends on an HTTP request, so the scheduler can use it too.
/// </summary>
public class DeviceCommandService
{
    /// <summary>A command nobody picked up / finished within this time is abandoned.</summary>
    public static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(5);

    private const int SimulatedUsagePercent = 5;
    private const int SimulatedMinimumLevel = 10;

    private readonly ApplicationDbContext _db;
    private readonly DeviceOptions _options;
    private readonly ILogger<DeviceCommandService> _logger;

    public DeviceCommandService(
        ApplicationDbContext db,
        IOptions<DeviceOptions> options,
        ILogger<DeviceCommandService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    private sealed record SprayUserSettings(
        int SprayDurationSeconds,
        bool SprayNotificationsEnabled,
        bool LowInsecticideAlertsEnabled,
        int LowInsecticideThreshold);

    // ── Queueing (web app / scheduler side) ───────────────────────────────────

    /// <summary>Queues a spray for the user's current sprayer and saves.</summary>
    public async Task<QueueSprayResult> QueueSprayAsync(string userId, string source, CancellationToken ct = default)
    {
        var result = await StageSprayAsync(userId, source, ct);
        await _db.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>Same as <see cref="QueueSprayAsync"/> but leaves saving to the caller so it can be atomic with other changes.</summary>
    public async Task<QueueSprayResult> StageSprayAsync(string userId, string source, CancellationToken ct = default)
    {
        var linked = await _db.LinkedDevices
            .FirstOrDefaultAsync(l => l.UserId == userId && l.IsCurrent, ct);
        if (linked is null)
        {
            return new QueueSprayResult(QueueSprayOutcome.NoDevice);
        }

        var now = DateTime.UtcNow;
        var active = await LoadActiveCommandsAsync(linked.DeviceId, now, ct);
        var queuedInThisUnitOfWork = _db.ChangeTracker.Entries<DeviceCommand>()
            .Any(e => e.State == EntityState.Added && e.Entity.DeviceId == linked.DeviceId);
        if (queuedInThisUnitOfWork || active.Any(c => c.Status is DeviceCommandStatus.Pending or DeviceCommandStatus.Sent))
        {
            return new QueueSprayResult(QueueSprayOutcome.Busy);
        }

        var device = await GetOrCreateDeviceStateAsync(linked, ct);
        var settings = await LoadSettingsAsync(userId, ct);
        var isScheduled = source == "Schedule";

        var command = new DeviceCommand
        {
            UserId = userId,
            DeviceId = linked.DeviceId,
            Type = DeviceCommandType.Spray,
            DurationSeconds = settings.SprayDurationSeconds,
            Status = DeviceCommandStatus.Pending,
            Source = source,
            CreatedAt = now
        };
        _db.DeviceCommands.Add(command);

        _db.SprayHistory.Add(new SprayHistoryItem
        {
            UserId = userId,
            Time = now,
            Type = isScheduled ? "Scheduled spray" : "Manual spray"
        });

        if (settings.SprayNotificationsEnabled)
        {
            AddNotification(userId,
                isScheduled ? "Scheduled Spray Started" : "Manual Spray Triggered",
                $"A spray cycle was sent to {device.Name}.",
                NotificationSeverity.Info,
                now);
        }

        if (_options.SimulateHardware)
        {
            ApplySprayResult(device, command, success: true, reportedLevel: null, settings, now);
        }

        return new QueueSprayResult(QueueSprayOutcome.Queued, command);
    }

    // ── Device side (called by the device API) ────────────────────────────────

    public async Task<LinkedDevice?> AuthenticateDeviceAsync(string? deviceId, string? apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrEmpty(apiKey))
        {
            return null;
        }

        var normalized = deviceId.Trim().ToUpperInvariant();
        var linked = await _db.LinkedDevices.FirstOrDefaultAsync(l => l.DeviceId == normalized, ct);
        if (linked is null || linked.ApiKeyHash.Length == 0)
        {
            return null;
        }

        return ApiKeyHasher.Verify(apiKey, linked.ApiKeyHash) ? linked : null;
    }

    public async Task RecordHeartbeatAsync(LinkedDevice linked, int? insecticideLevel, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var device = await GetOrCreateDeviceStateAsync(linked, ct);
        var previous = device.InsecticideLevel;

        device.LastSeen = now;
        linked.LastActiveAt = now;

        if (insecticideLevel is int level)
        {
            device.InsecticideLevel = Math.Clamp(level, 0, 100);
            var settings = await LoadSettingsAsync(linked.UserId, ct);
            NotifyIfLowLevelCrossed(linked.UserId, device, previous, settings, now);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Hands the oldest pending command to the sprayer (and marks it Sent), or returns null.</summary>
    public async Task<DeviceCommand?> ClaimNextCommandAsync(LinkedDevice linked, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var device = await GetOrCreateDeviceStateAsync(linked, ct);
        device.LastSeen = now; // polling is proof of life
        linked.LastActiveAt = now;

        var active = await LoadActiveCommandsAsync(linked.DeviceId, now, ct);
        var next = active
            .Where(c => c.Status == DeviceCommandStatus.Pending)
            .OrderBy(c => c.CreatedAt)
            .FirstOrDefault();

        if (next is not null)
        {
            next.Status = DeviceCommandStatus.Sent;
            next.SentAt = now;
        }

        await _db.SaveChangesAsync(ct);
        return next;
    }

    public async Task<bool> CompleteCommandAsync(
        LinkedDevice linked, int commandId, bool success, int? insecticideLevel, CancellationToken ct = default)
    {
        var command = await _db.DeviceCommands.FirstOrDefaultAsync(c =>
            c.Id == commandId && c.DeviceId == linked.DeviceId && c.Status == DeviceCommandStatus.Sent, ct);
        if (command is null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var device = await GetOrCreateDeviceStateAsync(linked, ct);
        var settings = await LoadSettingsAsync(linked.UserId, ct);

        ApplySprayResult(device, command, success, insecticideLevel, settings, now);
        linked.LastActiveAt = now;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Loads Pending/Sent commands for a sprayer, marking any that outlived the timeout as Expired.</summary>
    private async Task<List<DeviceCommand>> LoadActiveCommandsAsync(string deviceId, DateTime now, CancellationToken ct)
    {
        var active = await _db.DeviceCommands
            .Where(c => c.DeviceId == deviceId
                && (c.Status == DeviceCommandStatus.Pending || c.Status == DeviceCommandStatus.Sent))
            .ToListAsync(ct);

        foreach (var command in active.Where(c => now - c.CreatedAt > CommandTimeout))
        {
            command.Status = DeviceCommandStatus.Expired;
            _logger.LogWarning("Device command {CommandId} for {DeviceId} expired without completing", command.Id, deviceId);
        }

        return active;
    }

    private async Task<DeviceState> GetOrCreateDeviceStateAsync(LinkedDevice linked, CancellationToken ct)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == linked.DeviceId, ct);
        if (device is not null)
        {
            return device;
        }

        device = new DeviceState
        {
            UserId = linked.UserId,
            DeviceId = linked.DeviceId,
            Name = linked.Name,
            LastSeen = DateTime.UtcNow - DeviceState.OnlineWindow - TimeSpan.FromSeconds(1)
        };
        _db.Devices.Add(device);
        return device;
    }

    private async Task<SprayUserSettings> LoadSettingsAsync(string userId, CancellationToken ct)
    {
        var settings = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new SprayUserSettings(
                u.SprayDurationSeconds,
                u.SprayNotificationsEnabled,
                u.LowInsecticideAlertsEnabled,
                u.LowInsecticideThreshold))
            .FirstOrDefaultAsync(ct);

        return settings ?? new SprayUserSettings(30, true, true, 20);
    }

    private void ApplySprayResult(
        DeviceState device, DeviceCommand command, bool success, int? reportedLevel, SprayUserSettings settings, DateTime now)
    {
        command.Status = success ? DeviceCommandStatus.Completed : DeviceCommandStatus.Failed;
        command.CompletedAt = now;
        device.LastSeen = now;

        if (!success)
        {
            AddNotification(command.UserId, "Spray Failed",
                $"{device.Name} reported that the spray cycle did not complete.",
                NotificationSeverity.Warning, now);
            return;
        }

        var previous = device.InsecticideLevel;
        device.LastSprayAt = now;
        device.InsecticideLevel = reportedLevel is int reported
            ? Math.Clamp(reported, 0, 100)
            : Math.Min(previous, Math.Max(SimulatedMinimumLevel, previous - SimulatedUsagePercent));

        NotifyIfLowLevelCrossed(command.UserId, device, previous, settings, now);
    }

    private void NotifyIfLowLevelCrossed(
        string userId, DeviceState device, int previousLevel, SprayUserSettings settings, DateTime now)
    {
        if (settings.LowInsecticideAlertsEnabled
            && previousLevel > settings.LowInsecticideThreshold
            && device.InsecticideLevel <= settings.LowInsecticideThreshold)
        {
            AddNotification(userId, "Low Insecticide Level",
                $"Insecticide has reached {device.InsecticideLevel}%. Please refill soon.",
                NotificationSeverity.Warning, now);
        }
    }

    private void AddNotification(string userId, string title, string message, NotificationSeverity severity, DateTime now) =>
        _db.Notifications.Add(new NotificationItem
        {
            UserId = userId,
            Title = title,
            Message = message,
            CreatedAt = now,
            Severity = severity
        });
}

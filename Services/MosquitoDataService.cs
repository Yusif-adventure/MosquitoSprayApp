using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SmartMosquitoControl.Data;
using SmartMosquitoControl.Models;

namespace SmartMosquitoControl.Services;

public enum LinkDeviceOutcome { Linked, AlreadyLinked, NotSignedIn }

public sealed record LinkDeviceResult(LinkDeviceOutcome Outcome, LinkedDevice? Device = null, string? ApiKey = null);

/// <summary>Per-user data access. Every query is scoped to the signed-in user's id.</summary>
public class MosquitoDataService
{
    private const int HistoryLimit = 100;
    private const int NotificationLimit = 50;

    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly DeviceCommandService _commands;

    private ApplicationUser? _currentUser;
    private bool _currentUserLoaded;

    public MosquitoDataService(ApplicationDbContext db, IHttpContextAccessor http, DeviceCommandService commands)
    {
        _db = db;
        _http = http;
        _commands = commands;
    }

    public string? CurrentUserId =>
        _http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    // ── User ──────────────────────────────────────────────────────────────────

    /// <summary>Loaded once per request and reused.</summary>
    public async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        if (_currentUserLoaded)
        {
            return _currentUser;
        }

        var userId = CurrentUserId;
        if (userId is null)
        {
            return null;
        }

        _currentUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        _currentUserLoaded = true;
        return _currentUser;
    }

    public async Task UpdateSettingsAsync(
        int sprayDurationSeconds,
        int lowInsecticideThreshold,
        bool lowInsecticideAlertsEnabled,
        bool sprayNotificationsEnabled)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return;

        user.SprayDurationSeconds = Math.Clamp(sprayDurationSeconds, 10, 120);
        user.LowInsecticideThreshold = Math.Clamp(lowInsecticideThreshold, 5, 80);
        user.LowInsecticideAlertsEnabled = lowInsecticideAlertsEnabled;
        user.SprayNotificationsEnabled = sprayNotificationsEnabled;
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        await _db.SaveChangesAsync();
    }

    // ── Device ────────────────────────────────────────────────────────────────

    /// <summary>The state of the user's current (primary) sprayer.</summary>
    public async Task<DeviceState?> GetDeviceAsync()
    {
        var userId = CurrentUserId;
        if (userId is null) return null;

        var currentDeviceId = await _db.LinkedDevices
            .Where(l => l.UserId == userId && l.IsCurrent)
            .Select(l => l.DeviceId)
            .FirstOrDefaultAsync();
        if (currentDeviceId is null) return null;

        return await _db.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == currentDeviceId);
    }

    public async Task<QueueSprayResult> TriggerManualSprayAsync()
    {
        var userId = CurrentUserId;
        if (userId is null)
        {
            return new QueueSprayResult(QueueSprayOutcome.NoDevice);
        }

        return await _commands.QueueSprayAsync(userId, "Manual");
    }

    // ── Schedules ─────────────────────────────────────────────────────────────

    public async Task<List<ScheduleItem>> GetUpcomingSchedulesAsync(int? take = null)
    {
        var userId = CurrentUserId;
        if (userId is null) return new();

        var query = _db.Schedules
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.ScheduledFor > DateTime.UtcNow)
            .OrderBy(s => s.ScheduledFor)
            .AsQueryable();

        if (take is int limit)
        {
            query = query.Take(limit);
        }

        return await query.ToListAsync();
    }

    /// <summary>The next schedule that will actually fire (enabled, in the future).</summary>
    public async Task<ScheduleItem?> GetNextScheduleAsync()
    {
        var userId = CurrentUserId;
        if (userId is null) return null;

        return await _db.Schedules
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.IsEnabled && s.ScheduledFor > DateTime.UtcNow)
            .OrderBy(s => s.ScheduledFor)
            .FirstOrDefaultAsync();
    }

    public async Task<List<ScheduleItem>> GetScheduleHistoryAsync()
    {
        var userId = CurrentUserId;
        if (userId is null) return new();

        return await _db.Schedules
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.ScheduledFor <= DateTime.UtcNow)
            .OrderByDescending(s => s.ScheduledFor)
            .Take(HistoryLimit)
            .ToListAsync();
    }

    public async Task AddScheduleAsync(ScheduleItem item)
    {
        var userId = CurrentUserId;
        if (userId is null) return;
        item.UserId = userId;
        _db.Schedules.Add(item);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> SetScheduleEnabledAsync(int id, bool isEnabled)
    {
        var userId = CurrentUserId;
        if (userId is null) return false;
        var schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (schedule is null) return false;
        schedule.IsEnabled = isEnabled;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveScheduleAsync(int id)
    {
        var userId = CurrentUserId;
        if (userId is null) return false;
        var schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (schedule is null) return false;
        _db.Schedules.Remove(schedule);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Linked devices ────────────────────────────────────────────────────────

    public async Task<List<LinkedDevice>> GetLinkedDevicesAsync()
    {
        var userId = CurrentUserId;
        if (userId is null) return new();

        return await _db.LinkedDevices
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.IsCurrent)
            .ThenByDescending(d => d.LastActiveAt)
            .ToListAsync();
    }

    /// <summary>
    /// Pairs a sprayer. A sprayer ID can belong to only one account. On success the returned
    /// <see cref="LinkDeviceResult.ApiKey"/> is the sprayer's secret; it is not stored and cannot be shown again.
    /// </summary>
    public async Task<LinkDeviceResult> AddLinkedDeviceAsync(string deviceId, string name)
    {
        var userId = CurrentUserId;
        if (userId is null) return new LinkDeviceResult(LinkDeviceOutcome.NotSignedIn);

        var normalizedDeviceId = deviceId.Trim().ToUpperInvariant();
        if (await _db.LinkedDevices.AnyAsync(d => d.DeviceId == normalizedDeviceId))
        {
            return new LinkDeviceResult(LinkDeviceOutcome.AlreadyLinked);
        }

        // The first sprayer a user links becomes their primary one.
        var isFirstDevice = !await _db.LinkedDevices.AnyAsync(d => d.UserId == userId);
        var linkedAt = DateTime.UtcNow;
        var displayName = string.IsNullOrWhiteSpace(name) ? "IoT Sprayer" : name.Trim();
        var apiKey = ApiKeyHasher.Generate();

        var linkedDevice = new LinkedDevice
        {
            UserId = userId,
            Name = displayName,
            DeviceId = normalizedDeviceId,
            LinkedAt = linkedAt,
            LastActiveAt = linkedAt,
            IsCurrent = isFirstDevice,
            ApiKeyHash = ApiKeyHasher.Hash(apiKey)
        };
        _db.LinkedDevices.Add(linkedDevice);

        _db.Devices.Add(new DeviceState
        {
            UserId = userId,
            DeviceId = normalizedDeviceId,
            Name = displayName,
            InsecticideLevel = 100,
            // Offline until the sprayer checks in for the first time.
            LastSeen = linkedAt - DeviceState.OnlineWindow - TimeSpan.FromSeconds(1)
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Lost a race with someone else pairing the same sprayer ID (unique index).
            return new LinkDeviceResult(LinkDeviceOutcome.AlreadyLinked);
        }

        return new LinkDeviceResult(LinkDeviceOutcome.Linked, linkedDevice, apiKey);
    }

    public async Task<bool> SetCurrentDeviceAsync(int id)
    {
        var userId = CurrentUserId;
        if (userId is null) return false;

        var devices = await _db.LinkedDevices.Where(d => d.UserId == userId).ToListAsync();
        if (devices.All(d => d.Id != id)) return false;

        foreach (var device in devices)
        {
            device.IsCurrent = device.Id == id;
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveLinkedDeviceAsync(int id)
    {
        var userId = CurrentUserId;
        if (userId is null) return false;

        var device = await _db.LinkedDevices.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);
        if (device is null) return false;

        var wasCurrent = device.IsCurrent;
        _db.LinkedDevices.Remove(device);

        var states = await _db.Devices.Where(d => d.DeviceId == device.DeviceId && d.UserId == userId).ToListAsync();
        _db.Devices.RemoveRange(states);

        var openCommands = await _db.DeviceCommands
            .Where(c => c.DeviceId == device.DeviceId
                && (c.Status == DeviceCommandStatus.Pending || c.Status == DeviceCommandStatus.Sent))
            .ToListAsync();
        foreach (var command in openCommands)
        {
            command.Status = DeviceCommandStatus.Expired;
        }

        if (wasCurrent)
        {
            var next = await _db.LinkedDevices
                .Where(d => d.UserId == userId && d.Id != id)
                .OrderByDescending(d => d.LinkedAt)
                .FirstOrDefaultAsync();
            if (next is not null)
            {
                next.IsCurrent = true;
            }
        }

        await _db.SaveChangesAsync();
        return true;
    }

    // ── Spray history ─────────────────────────────────────────────────────────

    public async Task<List<SprayHistoryItem>> GetSprayHistoryAsync()
    {
        var userId = CurrentUserId;
        if (userId is null) return new();

        return await _db.SprayHistory
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.Time)
            .Take(HistoryLimit)
            .ToListAsync();
    }

    // ── Notifications ─────────────────────────────────────────────────────────

    public async Task<List<NotificationItem>> GetNotificationsAsync(int? take = null)
    {
        var userId = CurrentUserId;
        if (userId is null) return new();

        var query = _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .AsQueryable();

        query = query.Take(take ?? NotificationLimit);
        return await query.ToListAsync();
    }

    public async Task<int> CountUnreadNotificationsAsync()
    {
        var userId = CurrentUserId;
        if (userId is null) return 0;
        return await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task MarkAllNotificationsReadAsync()
    {
        var userId = CurrentUserId;
        if (userId is null) return;

        var unread = await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
        foreach (var notification in unread)
        {
            notification.IsRead = true;
        }

        if (unread.Count > 0)
        {
            await _db.SaveChangesAsync();
        }
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartMosquitoControl.Data;
using SmartMosquitoControl.Models;

namespace SmartMosquitoControl.Services;

public class MosquitoDataService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly UserManager<ApplicationUser> _userManager;

    public MosquitoDataService(
        ApplicationDbContext db,
        IHttpContextAccessor http,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _http = http;
        _userManager = userManager;
    }

    private string? CurrentUserId =>
        _http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    // ── Device ────────────────────────────────────────────────────────────────

    public async Task<DeviceState?> GetDeviceAsync()
    {
        var userId = CurrentUserId;
        if (userId is null) return null;
        return await _db.Devices.FirstOrDefaultAsync(d => d.UserId == userId);
    }

    // ── User settings ─────────────────────────────────────────────────────────

    public async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var userId = CurrentUserId;
        if (userId is null) return null;
        return await _userManager.FindByIdAsync(userId);
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
        await _userManager.UpdateAsync(user);
    }

    // ── Schedules ─────────────────────────────────────────────────────────────

    public async Task<List<ScheduleItem>> GetUpcomingSchedulesAsync()
    {
        var userId = CurrentUserId;
        if (userId is null) return new();
        return await _db.Schedules
            .Where(s => s.UserId == userId && s.ScheduledFor > DateTime.UtcNow)
            .OrderBy(s => s.ScheduledFor)
            .ToListAsync();
    }

    public async Task<List<ScheduleItem>> GetScheduleHistoryAsync()
    {
        var userId = CurrentUserId;
        if (userId is null) return new();
        return await _db.Schedules
            .Where(s => s.UserId == userId && s.ScheduledFor <= DateTime.UtcNow)
            .OrderByDescending(s => s.ScheduledFor)
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
        var schedule = await _db.Schedules
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (schedule is null) return false;
        schedule.IsEnabled = isEnabled;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveScheduleAsync(int id)
    {
        var userId = CurrentUserId;
        if (userId is null) return false;
        var schedule = await _db.Schedules
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
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
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.IsCurrent)
            .ThenByDescending(d => d.LastActiveAt)
            .ToListAsync();
    }

    public async Task<LinkedDevice?> AddLinkedDeviceAsync(string deviceId, string name)
    {
        var userId = CurrentUserId;
        if (userId is null) return null;

        var normalizedDeviceId = deviceId.Trim().ToUpperInvariant();
        var exists = await _db.LinkedDevices.AnyAsync(d =>
            d.UserId == userId &&
            d.DeviceId == normalizedDeviceId);
        if (exists) return null;

        // First device linked becomes the primary device
        var isFirstDevice = !await _db.LinkedDevices.AnyAsync(d => d.UserId == userId);
        var linkedAt = DateTime.UtcNow;
        var linkedDevice = new LinkedDevice
        {
            UserId = userId,
            Name = string.IsNullOrWhiteSpace(name) ? "IoT Sprayer" : name.Trim(),
            DeviceId = normalizedDeviceId,
            LinkedAt = linkedAt,
            LastActiveAt = linkedAt,
            IsCurrent = isFirstDevice
        };

        _db.LinkedDevices.Add(linkedDevice);

        // Auto-create a DeviceState record for the first linked sprayer
        if (isFirstDevice)
        {
            var hasDevice = await _db.Devices.AnyAsync(d => d.UserId == userId);
            if (!hasDevice)
            {
                _db.Devices.Add(new DeviceState
                {
                    UserId = userId,
                    DeviceId = normalizedDeviceId,
                    Name = linkedDevice.Name,
                    IsOnline = false,
                    InsecticideLevel = 100,
                    LastSeen = DateTime.UtcNow,
                    Location = "Living Room"
                });
            }
        }

        await _db.SaveChangesAsync();
        return linkedDevice;
    }

    public async Task<bool> RemoveLinkedDeviceAsync(int id)
    {
        var userId = CurrentUserId;
        if (userId is null) return false;
        var device = await _db.LinkedDevices
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId && !d.IsCurrent);
        if (device is null) return false;
        _db.LinkedDevices.Remove(device);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Spray history ─────────────────────────────────────────────────────────

    public async Task<List<SprayHistoryItem>> GetSprayHistoryAsync()
    {
        var userId = CurrentUserId;
        if (userId is null) return new();
        return await _db.SprayHistory
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.Time)
            .ToListAsync();
    }

    // ── Notifications ─────────────────────────────────────────────────────────

    public async Task<List<NotificationItem>> GetNotificationsAsync()
    {
        var userId = CurrentUserId;
        if (userId is null) return new();
        return await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    // ── Manual spray ──────────────────────────────────────────────────────────

    public async Task TriggerManualSprayAsync()
    {
        var userId = CurrentUserId;
        if (userId is null) return;

        var user = await GetCurrentUserAsync();
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.UserId == userId);

        if (device is not null)
        {
            var previousLevel = device.InsecticideLevel;
            var sprayedAt = DateTime.UtcNow;
            device.LastSprayAt = sprayedAt;
            device.InsecticideLevel = Math.Max(10, device.InsecticideLevel - 5);
            device.LastSeen = sprayedAt;

            _db.SprayHistory.Add(new SprayHistoryItem
            {
                UserId = userId,
                Time = sprayedAt,
                Type = "Manual spray"
            });

            if (user?.SprayNotificationsEnabled == true)
            {
                _db.Notifications.Add(new NotificationItem
                {
                    UserId = userId,
                    Title = "Manual Spray Triggered",
                    Message = "A manual spray cycle was triggered successfully.",
                    CreatedAt = DateTime.UtcNow,
                    Severity = NotificationSeverity.Info
                });
            }

            if (user?.LowInsecticideAlertsEnabled == true
                && previousLevel > (user?.LowInsecticideThreshold ?? 20)
                && device.InsecticideLevel <= (user?.LowInsecticideThreshold ?? 20))
            {
                _db.Notifications.Add(new NotificationItem
                {
                    UserId = userId,
                    Title = "Low Insecticide Level",
                    Message = $"Insecticide has reached {device.InsecticideLevel}%. Please refill soon.",
                    CreatedAt = DateTime.UtcNow,
                    Severity = NotificationSeverity.Warning
                });
            }

            await _db.SaveChangesAsync();
        }
    }
}

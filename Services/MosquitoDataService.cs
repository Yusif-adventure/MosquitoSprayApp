using SmartMosquitoControl.Models;

namespace SmartMosquitoControl.Services;

public class MosquitoDataService
{
    public DeviceState Device { get; } = new();
    public List<LinkedDevice> LinkedDevices { get; } = new();
    public List<ScheduleItem> Schedules { get; } = new();
    public List<ScheduleItem> ScheduleHistory { get; } = new();
    public List<SprayHistoryItem> SprayHistory { get; } = new();
    public List<NotificationItem> Notifications { get; } = new();
    public int SprayDurationSeconds { get; private set; } = 30;
    public int LowInsecticideThreshold { get; private set; } = 20;
    public bool LowInsecticideAlertsEnabled { get; private set; } = true;
    public bool SprayNotificationsEnabled { get; private set; } = true;

    public MosquitoDataService()
    {
        var now = DateTime.Now;
        LinkedDevices.Add(new LinkedDevice
        {
            Id = 1,
            Name = Device.Name,
            DeviceId = Device.DeviceId,
            LinkedAt = now,
            LastActiveAt = now,
            IsCurrent = true
        });

        var today = DateTime.Today;
        var nextEvening = today.AddDays(DateTime.Now.TimeOfDay >= TimeSpan.FromHours(19) ? 1 : 0).AddHours(19);
        var nextMorning = today.AddDays(DateTime.Now.TimeOfDay >= TimeSpan.FromHours(10) ? 1 : 0).AddHours(10);
        var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)today.DayOfWeek + 7) % 7;
        var nextSaturday = today.AddDays(daysUntilSaturday == 0 ? 7 : daysUntilSaturday).AddHours(18);
        var spareCycle = today.AddDays(2).AddHours(9);

        Schedules.AddRange(new[]
        {
            new ScheduleItem { Id = 1, Name = "Evening Protection", Description = "Every day • 7:00 PM", ScheduledFor = nextEvening, IsEnabled = true, IsCustom = false },
            new ScheduleItem { Id = 2, Name = "Morning Refresh", Description = "Every day • 10:00 AM", ScheduledFor = nextMorning, IsEnabled = true, IsCustom = false },
            new ScheduleItem { Id = 3, Name = "Weekend Spray", Description = nextSaturday.ToString("ddd, MMM d • h:mm tt"), ScheduledFor = nextSaturday, IsEnabled = true, IsCustom = true },
            new ScheduleItem { Id = 4, Name = "Spare Cycle", Description = spareCycle.ToString("ddd, MMM d • h:mm tt"), ScheduledFor = spareCycle, IsEnabled = false, IsCustom = true }
        });

    }

    public void AddSchedule(ScheduleItem item)
    {
        item.Id = Schedules.Count == 0 ? 1 : Schedules.Max(x => x.Id) + 1;
        item.IsEnabled = true;
        Schedules.Insert(0, item);
    }

    public bool SetScheduleEnabled(int id, bool isEnabled)
    {
        var schedule = Schedules.FirstOrDefault(item => item.Id == id);
        if (schedule is null)
        {
            return false;
        }

        schedule.IsEnabled = isEnabled;
        return true;
    }

    public bool RemoveSchedule(int id)
    {
        var schedule = Schedules.FirstOrDefault(item => item.Id == id);
        return schedule is not null && Schedules.Remove(schedule);
    }

    public LinkedDevice? AddLinkedDevice(string deviceId, string name)
    {
        var normalizedDeviceId = deviceId.Trim().ToUpperInvariant();
        if (LinkedDevices.Any(device => string.Equals(device.DeviceId, normalizedDeviceId, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var linkedAt = DateTime.Now;
        var linkedDevice = new LinkedDevice
        {
            Id = LinkedDevices.Count == 0 ? 1 : LinkedDevices.Max(device => device.Id) + 1,
            Name = string.IsNullOrWhiteSpace(name) ? "IoT Sprayer" : name.Trim(),
            DeviceId = normalizedDeviceId,
            LinkedAt = linkedAt,
            LastActiveAt = linkedAt
        };

        LinkedDevices.Insert(0, linkedDevice);
        return linkedDevice;
    }

    public bool RemoveLinkedDevice(int id)
    {
        var linkedDevice = LinkedDevices.FirstOrDefault(device => device.Id == id && !device.IsCurrent);
        return linkedDevice is not null && LinkedDevices.Remove(linkedDevice);
    }

    public void UpdateSettings(int sprayDurationSeconds, int lowInsecticideThreshold, bool lowInsecticideAlertsEnabled, bool sprayNotificationsEnabled)
    {
        SprayDurationSeconds = Math.Clamp(sprayDurationSeconds, 10, 120);
        LowInsecticideThreshold = Math.Clamp(lowInsecticideThreshold, 5, 80);
        LowInsecticideAlertsEnabled = lowInsecticideAlertsEnabled;
        SprayNotificationsEnabled = sprayNotificationsEnabled;
    }

    public void TriggerManualSpray()
    {
        var previousLevel = Device.InsecticideLevel;
        var sprayedAt = DateTime.Now;
        Device.LastSprayAt = sprayedAt;
        Device.InsecticideLevel = Math.Max(10, Device.InsecticideLevel - 5);
        Device.LastSeen = sprayedAt;
        SprayHistory.Insert(0, new SprayHistoryItem
        {
            Time = sprayedAt,
            Type = "Manual spray"
        });

        if (SprayNotificationsEnabled)
        {
            Notifications.Insert(0, new NotificationItem
            {
                Id = Notifications.Count == 0 ? 1 : Notifications.Max(x => x.Id) + 1,
                Title = "Manual Spray Triggered",
                Message = "A manual spray cycle was triggered successfully.",
                CreatedAt = DateTime.Now,
                Severity = "info"
            });
        }

        if (LowInsecticideAlertsEnabled && previousLevel > LowInsecticideThreshold && Device.InsecticideLevel <= LowInsecticideThreshold)
        {
            Notifications.Insert(0, new NotificationItem
            {
                Id = Notifications.Count == 0 ? 1 : Notifications.Max(x => x.Id) + 1,
                Title = "Low Insecticide Level",
                Message = $"Insecticide has reached {Device.InsecticideLevel}%. Please refill soon.",
                CreatedAt = DateTime.Now,
                Severity = "warning"
            });
        }
    }
}

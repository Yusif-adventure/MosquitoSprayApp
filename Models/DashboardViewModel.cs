namespace SmartMosquitoControl.Models;

public class DashboardViewModel
{
    public DeviceState? Device { get; set; }
    public List<ScheduleItem> Schedules { get; set; } = new();
    public List<NotificationItem> Notifications { get; set; } = new();
    public string UserFirstName { get; set; } = string.Empty;
    public string UserAvatarLetter { get; set; } = "?";
    public string Greeting { get; set; } = "Hello";
    public ScheduleItem? NextSchedule { get; set; }
    public int UnreadNotificationCount { get; set; }
}

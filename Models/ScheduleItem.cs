namespace SmartMosquitoControl.Models;

public class ScheduleItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime ScheduledFor { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsCustom { get; set; }
}

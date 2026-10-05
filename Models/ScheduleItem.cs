namespace SmartMosquitoControl.Models;

public enum ScheduleStatus
{
    /// <summary>Waiting for its time (or, if disabled, skipped when the time passes).</summary>
    Pending = 0,
    /// <summary>The scheduler queued a spray command for the sprayer.</summary>
    Executed = 1,
    /// <summary>The time passed without a spray being queued (app down, no sprayer, or too late).</summary>
    Missed = 2
}

public class ScheduleItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Always stored in UTC. Views convert to the viewer's local time in the browser.</summary>
    public DateTime ScheduledFor { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsCustom { get; set; }
    public ScheduleStatus Status { get; set; } = ScheduleStatus.Pending;
    public DateTime? ExecutedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign key
    public ApplicationUser? User { get; set; }
}

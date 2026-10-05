namespace SmartMosquitoControl.Models;

public enum DeviceCommandType { Spray = 0 }

public enum DeviceCommandStatus
{
    /// <summary>Queued; waiting for the sprayer to poll for it.</summary>
    Pending = 0,
    /// <summary>Handed to the sprayer; waiting for it to report back.</summary>
    Sent = 1,
    Completed = 2,
    Failed = 3,
    /// <summary>Never picked up / never reported back within the timeout.</summary>
    Expired = 4
}

/// <summary>A command the server wants a sprayer to carry out. Sprayers poll the device API for these.</summary>
public class DeviceCommand
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public DeviceCommandType Type { get; set; } = DeviceCommandType.Spray;
    public int DurationSeconds { get; set; } = 30;
    public DeviceCommandStatus Status { get; set; } = DeviceCommandStatus.Pending;

    /// <summary>"Manual" or "Schedule".</summary>
    public string Source { get; set; } = "Manual";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Foreign key
    public ApplicationUser? User { get; set; }
}

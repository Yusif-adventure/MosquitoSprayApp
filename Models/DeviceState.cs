using System.ComponentModel.DataAnnotations.Schema;

namespace SmartMosquitoControl.Models;

public class DeviceState
{
    /// <summary>A sprayer that has not checked in within this window is considered offline.</summary>
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(2);

    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = "IoT Sprayer";
    public int InsecticideLevel { get; set; } = 100;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public DateTime? LastSprayAt { get; set; }
    public string Location { get; set; } = "Living Room";

    /// <summary>Derived from the last heartbeat rather than stored, so it can never go stale.</summary>
    [NotMapped]
    public bool IsOnline => DateTime.UtcNow - LastSeen <= OnlineWindow;

    // Foreign key
    public ApplicationUser? User { get; set; }
}

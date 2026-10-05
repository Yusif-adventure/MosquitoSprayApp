namespace SmartMosquitoControl.Models;

public class LinkedDevice
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    /// <summary>The sprayer the dashboard, manual spray and schedules act on.</summary>
    public bool IsCurrent { get; set; }

    /// <summary>
    /// SHA-256 hash of the secret the sprayer uses to authenticate against the device API.
    /// The plain key is shown to the user once, at pairing time, and never stored.
    /// </summary>
    public string ApiKeyHash { get; set; } = string.Empty;

    // Foreign key
    public ApplicationUser? User { get; set; }
}

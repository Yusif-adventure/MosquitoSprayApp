namespace SmartMosquitoControl.Models;

public class LinkedDevice
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
    public bool IsCurrent { get; set; }
    
    // Foreign key
    public ApplicationUser? User { get; set; }
}

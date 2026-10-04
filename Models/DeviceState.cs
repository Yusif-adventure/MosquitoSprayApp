namespace SmartMosquitoControl.Models;

public class DeviceState
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = "DEMO-SPR-001";
    public string Name { get; set; } = "Living Room Device";
    public bool IsOnline { get; set; } = true;
    public int InsecticideLevel { get; set; } = 65;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public DateTime? LastSprayAt { get; set; }
    public string Location { get; set; } = "Living Room";
    
    // Foreign key
    public ApplicationUser? User { get; set; }
}

namespace SmartMosquitoControl.Models;

public class DeviceState
{
    public string DeviceId { get; set; } = "DEMO-SPR-001";
    public string Name { get; set; } = "Living Room Device";
    public bool IsOnline { get; set; } = true;
    public int InsecticideLevel { get; set; } = 65;
    public DateTime LastSeen { get; set; } = DateTime.Now;
    public DateTime? LastSprayAt { get; set; }
    public string Location { get; set; } = "Living Room";
}

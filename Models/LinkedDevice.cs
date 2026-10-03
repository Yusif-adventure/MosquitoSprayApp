namespace SmartMosquitoControl.Models;

public class LinkedDevice
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public DateTime LinkedAt { get; set; }
    public DateTime LastActiveAt { get; set; }
    public bool IsCurrent { get; set; }
}

namespace SmartMosquitoControl.Models;

public class MonitoringViewModel
{
    public DeviceState? Device { get; set; }
    public IEnumerable<SprayHistoryItem> History { get; set; } = Array.Empty<SprayHistoryItem>();
}

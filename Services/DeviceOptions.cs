namespace SmartMosquitoControl.Services;

public class DeviceOptions
{
    /// <summary>
    /// When true there is no physical sprayer: spray commands are completed instantly by the server
    /// (insecticide level drops, device shows online). Defaults to true in Development, false otherwise.
    /// </summary>
    public bool SimulateHardware { get; set; }
}

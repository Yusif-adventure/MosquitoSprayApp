using System.ComponentModel.DataAnnotations;

namespace SmartMosquitoControl.Models;

public class SettingsViewModel
{
    [Range(10, 120)]
    [Display(Name = "Spray duration")]
    public int SprayDurationSeconds { get; set; } = 30;

    [Range(5, 80)]
    [Display(Name = "Alert when insecticide reaches")]
    public int LowInsecticideThreshold { get; set; }

    [Display(Name = "Low-level alerts")]
    public bool LowInsecticideAlertsEnabled { get; set; }

    [Display(Name = "Spray activity notifications")]
    public bool SprayNotificationsEnabled { get; set; }
}

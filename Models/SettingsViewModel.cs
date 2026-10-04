using System.ComponentModel.DataAnnotations;

namespace SmartMosquitoControl.Models;

public class SettingsViewModel
{
    [Range(10, 120, ErrorMessage = "Spray duration must be between 10 and 120 seconds")]
    [Display(Name = "Spray duration (seconds)")]
    public int SprayDurationSeconds { get; set; } = 30;

    [Range(5, 80, ErrorMessage = "Insecticide threshold must be between 5 and 80 percent")]
    [Display(Name = "Alert when insecticide reaches (%)")]
    public int LowInsecticideThreshold { get; set; } = 20;

    [Display(Name = "Enable low-level alerts")]
    public bool LowInsecticideAlertsEnabled { get; set; } = true;

    [Display(Name = "Enable spray activity notifications")]
    public bool SprayNotificationsEnabled { get; set; } = true;
}

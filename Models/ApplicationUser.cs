using Microsoft.AspNetCore.Identity;

namespace SmartMosquitoControl.Models;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Per-user spray settings
    public int SprayDurationSeconds { get; set; } = 30;
    public int LowInsecticideThreshold { get; set; } = 20;
    public bool LowInsecticideAlertsEnabled { get; set; } = true;
    public bool SprayNotificationsEnabled { get; set; } = true;
}

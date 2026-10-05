namespace SmartMosquitoControl.Models;

public enum NotificationSeverity { Info, Warning, Success }

public class NotificationItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
    public bool IsRead { get; set; } = false;

    // Foreign key
    public ApplicationUser? User { get; set; }

    // Convenience string for CSS class usage in views
    public string SeverityCssClass => Severity switch
    {
        NotificationSeverity.Warning => "warning",
        NotificationSeverity.Success => "success",
        _ => "info"
    };
}

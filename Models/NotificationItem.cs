namespace SmartMosquitoControl.Models;

public class NotificationItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Severity { get; set; } = "info";
}

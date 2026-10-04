namespace SmartMosquitoControl.Models;

public class SprayHistoryItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime Time { get; set; } = DateTime.UtcNow;
    public string Type { get; set; } = string.Empty;
    
    // Foreign key
    public ApplicationUser? User { get; set; }
}
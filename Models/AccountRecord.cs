namespace FinSyncNexus.Models;

public class AccountRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Code { get; set; }
    public DateTime CreatedAt { get; set; }
}

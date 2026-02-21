namespace FinSyncNexus.Models;

public class ExpenseRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string VendorName { get; set; } = "-";
    public string Category { get; set; } = "-";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Recorded";
    public DateTime CreatedAt { get; set; }
}

namespace FinSyncNexus.ViewModels;

public class ExpenseItem
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Vendor { get; set; } = "-";
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Recorded";
    public string Provider { get; set; } = string.Empty;
}

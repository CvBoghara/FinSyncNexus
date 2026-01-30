namespace FinSyncNexus.ViewModels;

public class PaymentItem
{
    public DateTime Date { get; set; }
    public string Customer { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = "-";
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Unknown";
    public string Reference { get; set; } = "-";
    public string Provider { get; set; } = string.Empty;
}

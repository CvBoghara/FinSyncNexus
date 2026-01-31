namespace FinSyncNexus.Models;

public class PaymentRecord
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = "-";
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Unknown";
    public string Reference { get; set; } = "-";
    public DateTime CreatedAt { get; set; }
}

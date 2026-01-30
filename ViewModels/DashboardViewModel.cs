using FinSyncNexus.Models;

namespace FinSyncNexus.ViewModels;

public class DashboardViewModel
{
    public decimal TotalRevenue { get; set; }
    public int TotalInvoices { get; set; }
    public int TotalCustomers { get; set; }
    public decimal OutstandingAmount { get; set; }
    public List<InvoiceRecord> RecentInvoices { get; set; } = new();
    public List<ConnectionStatus> Connections { get; set; } = new();
}

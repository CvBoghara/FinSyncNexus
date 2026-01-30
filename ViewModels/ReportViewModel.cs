namespace FinSyncNexus.ViewModels;

public class ReportViewModel
{
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit => TotalIncome - TotalExpenses;
    public List<ReportLineItem> ExpenseBreakdown { get; set; } = new();
}

public class ReportLineItem
{
    public string Account { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

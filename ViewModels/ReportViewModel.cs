namespace FinSyncNexus.ViewModels;

public class ReportViewModel
{
    public string ReportTitle { get; set; } = "Profit & Loss Report";
    public FilterViewModel Filters { get; set; } = new();
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit => TotalIncome - TotalExpenses;
    public decimal CashBalance { get; set; }
    public decimal XeroIncome { get; set; }
    public decimal QboIncome { get; set; }
    public decimal XeroExpense { get; set; }
    public decimal QboExpense { get; set; }
    public List<ReportLineItem> ExpenseBreakdown { get; set; } = new();
    public List<ReportTrendPoint> ProfitLossTrend { get; set; } = new();
    public List<ReportTrendPoint> CashFlowTrend { get; set; } = new();
    public List<ReportTopRecord> TopCustomers { get; set; } = new();
    public List<ReportTopRecord> TopVendors { get; set; } = new();
    public string HighestExpenseCategory { get; set; } = "-";
    public List<ReportTransaction> Transactions { get; set; } = new();
    public List<string> Alerts { get; set; } = new();
}

public class ReportLineItem
{
    public string Account { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class ReportTrendPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal Net => Income - Expense;
    public decimal Inflow { get; set; }
    public decimal Outflow { get; set; }
}

public class ReportTopRecord
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class ReportTransaction
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

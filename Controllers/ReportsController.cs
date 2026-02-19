using FinSyncNexus.Data;
using FinSyncNexus.Helpers;
using FinSyncNexus.Reports;
using FinSyncNexus.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;

namespace FinSyncNexus.Controllers;

public class ReportsController : Controller
{
    private readonly AppDbContext _db;

    public ReportsController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(FilterViewModel filters)
    {
        filters = FilterEngine.Normalize(filters);
        var viewModel = await BuildReportAsync(filters);
        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> GenerateReport(FilterViewModel filters)
    {
        filters = FilterEngine.Normalize(filters);
        var viewModel = await BuildReportAsync(filters);

        var csvLines = new List<string>
        {
            "Section,Label,Amount",
            $"Summary,Total Income,{viewModel.TotalIncome}",
            $"Summary,Total Expenses,{viewModel.TotalExpenses}",
            $"Summary,Net Profit,{viewModel.NetProfit}"
        };

        foreach (var item in viewModel.ExpenseBreakdown)
        {
            csvLines.Add($"Expense,{item.Account},{item.Amount}");
        }

        var content = string.Join(Environment.NewLine, csvLines);
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var fileName = $"FinSync_Report_{viewModel.Filters.FromDate:yyyyMMdd}_{viewModel.Filters.ToDate:yyyyMMdd}.csv";

        return File(bytes, "text/csv", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(FilterViewModel filters)
    {
        filters = FilterEngine.Normalize(filters);
        var viewModel = await BuildReportAsync(filters);

        var lines = new List<string>
        {
            "Date,Description,Account,Amount,Source,Type"
        };

        foreach (var item in viewModel.Transactions)
        {
            lines.Add($"{item.Date:yyyy-MM-dd},{EscapeCsv(item.Description)},{EscapeCsv(item.Account)},{item.Amount},{item.Source},{item.Type}");
        }

        var content = string.Join(Environment.NewLine, lines);
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var fileName = $"FinSync_Transactions_{filters.FromDate:yyyyMMdd}_{filters.ToDate:yyyyMMdd}.csv";

        return File(bytes, "text/csv", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf(FilterViewModel filters)
    {
        filters = FilterEngine.Normalize(filters);
        var viewModel = await BuildReportAsync(filters);

        var document = new ReportPdfDocument(viewModel);
        var pdf = document.GeneratePdf();
        var fileName = $"FinSync_Report_{filters.FromDate:yyyyMMdd}_{filters.ToDate:yyyyMMdd}.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    private async Task<ReportViewModel> BuildReportAsync(FilterViewModel filters)
    {
        var invoiceQuery = FilterEngine.ApplyInvoiceFilters(_db.Invoices.AsNoTracking(), filters);
        var expenseQuery = FilterEngine.ApplyExpenseFilters(_db.Expenses.AsNoTracking(), filters);
        var paymentQuery = FilterEngine.ApplyPaymentFilters(_db.Payments.AsNoTracking(), filters);

        var invoices = await invoiceQuery.ToListAsync();
        var expenses = await expenseQuery.ToListAsync();
        var payments = await paymentQuery.ToListAsync();

        var totalIncome = invoices.Sum(i => i.Amount);
        var totalExpenses = expenses.Sum(e => e.Amount);
        var cashBalance = payments.Sum(p => p.Amount) - totalExpenses;

        var viewModel = new ReportViewModel
        {
            Filters = filters,
            TotalIncome = totalIncome,
            TotalExpenses = totalExpenses,
            CashBalance = cashBalance,
            XeroIncome = invoices.Where(i => i.Provider == "Xero").Sum(i => i.Amount),
            QboIncome = invoices.Where(i => i.Provider == "QBO").Sum(i => i.Amount),
            XeroExpense = expenses.Where(e => e.Provider == "Xero").Sum(e => e.Amount),
            QboExpense = expenses.Where(e => e.Provider == "QBO").Sum(e => e.Amount)
        };

        viewModel.ExpenseBreakdown = expenses
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Category) ? "Uncategorized" : e.Category)
            .Select(group => new ReportLineItem
            {
                Account = group.Key,
                Amount = group.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        viewModel.HighestExpenseCategory = viewModel.ExpenseBreakdown.FirstOrDefault()?.Account ?? "-";

        viewModel.ProfitLossTrend = BuildMonthlyTrend(filters, invoices, expenses);
        viewModel.CashFlowTrend = BuildCashFlowTrend(filters, payments, expenses);

        viewModel.TopCustomers = invoices
            .GroupBy(i => i.CustomerName)
            .Select(group => new ReportTopRecord
            {
                Name = string.IsNullOrWhiteSpace(group.Key) ? "-" : group.Key,
                Amount = group.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.Amount)
            .Take(5)
            .ToList();

        viewModel.TopVendors = expenses
            .GroupBy(e => e.VendorName)
            .Select(group => new ReportTopRecord
            {
                Name = string.IsNullOrWhiteSpace(group.Key) ? "-" : group.Key,
                Amount = group.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.Amount)
            .Take(5)
            .ToList();

        viewModel.Transactions = BuildTransactions(expenses, payments);
        viewModel.Alerts = BuildAlerts(viewModel, expenses, invoices);

        return viewModel;
    }

    private static List<ReportTrendPoint> BuildMonthlyTrend(
        FilterViewModel filters,
        List<Models.InvoiceRecord> invoices,
        List<Models.ExpenseRecord> expenses)
    {
        var points = new List<ReportTrendPoint>();
        var start = filters.FromDate ?? DateTime.Today.AddDays(-30);
        var end = filters.ToDate ?? DateTime.Today;
        var month = new DateTime(start.Year, start.Month, 1);
        var lastMonth = new DateTime(end.Year, end.Month, 1);

        while (month <= lastMonth)
        {
            var label = month.ToString("MMM yyyy");
            var income = invoices.Where(i => GetInvoiceDate(i).Year == month.Year &&
                                              GetInvoiceDate(i).Month == month.Month)
                .Sum(i => i.Amount);
            var expense = expenses.Where(e => e.Date.Year == month.Year &&
                                              e.Date.Month == month.Month)
                .Sum(e => e.Amount);

            points.Add(new ReportTrendPoint
            {
                Label = label,
                Income = income,
                Expense = expense
            });

            month = month.AddMonths(1);
        }

        return points;
    }

    private static List<ReportTrendPoint> BuildCashFlowTrend(
        FilterViewModel filters,
        List<Models.PaymentRecord> payments,
        List<Models.ExpenseRecord> expenses)
    {
        var points = new List<ReportTrendPoint>();
        var start = filters.FromDate ?? DateTime.Today.AddDays(-30);
        var end = filters.ToDate ?? DateTime.Today;
        var month = new DateTime(start.Year, start.Month, 1);
        var lastMonth = new DateTime(end.Year, end.Month, 1);

        while (month <= lastMonth)
        {
            var label = month.ToString("MMM yyyy");
            var inflow = payments.Where(p => p.Date.Year == month.Year &&
                                             p.Date.Month == month.Month)
                .Sum(p => p.Amount);
            var outflow = expenses.Where(e => e.Date.Year == month.Year &&
                                              e.Date.Month == month.Month)
                .Sum(e => e.Amount);

            points.Add(new ReportTrendPoint
            {
                Label = label,
                Inflow = inflow,
                Outflow = outflow
            });

            month = month.AddMonths(1);
        }

        return points;
    }

    private static List<ReportTransaction> BuildTransactions(
        List<Models.ExpenseRecord> expenses,
        List<Models.PaymentRecord> payments)
    {
        var list = new List<ReportTransaction>();

        list.AddRange(payments.Select(payment => new ReportTransaction
        {
            Date = payment.Date,
            Description = $"Payment from {payment.CustomerName} ({payment.InvoiceNumber})",
            Account = payment.Method,
            Amount = payment.Amount,
            Source = payment.Provider,
            Type = "Inflow"
        }));

        list.AddRange(expenses.Select(expense => new ReportTransaction
        {
            Date = expense.Date,
            Description = expense.Description,
            Account = expense.Category,
            Amount = expense.Amount,
            Source = expense.Provider,
            Type = "Outflow"
        }));

        return list.OrderByDescending(x => x.Date).ToList();
    }

    private static List<string> BuildAlerts(
        ReportViewModel viewModel,
        List<Models.ExpenseRecord> expenses,
        List<Models.InvoiceRecord> invoices)
    {
        var alerts = new List<string>();
        const decimal monthlyLimit = 5000m;
        const decimal largeTransaction = 10000m;

        if (viewModel.TotalExpenses > viewModel.TotalIncome)
        {
            alerts.Add("Warning: Total expenses exceeded income for the selected period.");
        }

        var monthlyExpense = expenses
            .GroupBy(e => new { e.Date.Year, e.Date.Month })
            .OrderByDescending(x => x.Key.Year)
            .ThenByDescending(x => x.Key.Month)
            .FirstOrDefault();

        if (monthlyExpense != null && monthlyExpense.Sum(x => x.Amount) > monthlyLimit)
        {
            alerts.Add($"Alert: Monthly expenses exceeded the limit of {monthlyLimit:C}.");
        }

        var maxExpense = expenses.OrderByDescending(e => e.Amount).FirstOrDefault();
        var maxIncome = invoices.OrderByDescending(i => i.Amount).FirstOrDefault();
        if ((maxExpense?.Amount ?? 0m) > largeTransaction || (maxIncome?.Amount ?? 0m) > largeTransaction)
        {
            alerts.Add($"Alert: Large transaction detected above {largeTransaction:C}.");
        }

        if (!alerts.Any())
        {
            alerts.Add("No alerts triggered for the current filters.");
        }

        return alerts;
    }

    private static DateTime GetInvoiceDate(Models.InvoiceRecord invoice)
    {
        return invoice.DueDate ?? invoice.CreatedAt;
    }

    private static string EscapeCsv(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        if (input.Contains(',') || input.Contains('"') || input.Contains('\n'))
        {
            return $"\"{input.Replace("\"", "\"\"")}\"";
        }

        return input;
    }
}

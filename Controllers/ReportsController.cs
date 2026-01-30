using FinSyncNexus.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace FinSyncNexus.Controllers;

public class ReportsController : Controller
{
    public IActionResult Index(string? fromDate, string? toDate)
    {
        var (start, end) = ResolveDates(fromDate, toDate);
        var viewModel = BuildReport(start, end);

        ViewBag.FromDate = start.ToString("yyyy-MM-dd");
        ViewBag.ToDate = end.ToString("yyyy-MM-dd");

        return View(viewModel);
    }

    [HttpPost]
    public IActionResult GenerateReport(DateTime fromDate, DateTime toDate)
    {
        var viewModel = BuildReport(fromDate, toDate);

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
        var fileName = $"FinSync_Report_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.csv";

        return File(bytes, "text/csv", fileName);
    }

    private static (DateTime Start, DateTime End) ResolveDates(string? fromDate, string? toDate)
    {
        var end = DateTime.Today;
        var start = end.AddDays(-30);

        if (DateTime.TryParse(fromDate, out var fromValue))
        {
            start = fromValue;
        }

        if (DateTime.TryParse(toDate, out var toValue))
        {
            end = toValue;
        }

        if (end < start)
        {
            (start, end) = (end, start);
        }

        return (start, end);
    }

    private static ReportViewModel BuildReport(DateTime start, DateTime end)
    {
        _ = start;
        _ = end;

        return new ReportViewModel
        {
            TotalIncome = 0.00m,
            TotalExpenses = 158.85m,
            ExpenseBreakdown = new List<ReportLineItem>
            {
                new() { Account = "Automobile", Amount = 73.98m },
                new() { Account = "Meals and Entertainment", Amount = 18.97m }
            }
        };
    }
}

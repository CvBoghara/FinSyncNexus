using FinSyncNexus.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace FinSyncNexus.Controllers;

public class ExpensesController : Controller
{
    public IActionResult Index(string? provider)
    {
        var data = BuildDummyExpenses();

        if (!string.IsNullOrWhiteSpace(provider) && provider != "All")
        {
            data = data.Where(e => e.Provider == provider).ToList();
        }

        ViewBag.Provider = provider ?? "All";
        return View(data);
    }

    private List<ExpenseItem> BuildDummyExpenses()
    {
        return new List<ExpenseItem>
        {
            new() { Date = DateTime.Today.AddDays(-8), Description = "Expense", Vendor = "-", Category = "Mastercard", Amount = 34.00m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-19), Description = "Monthly Payment", Vendor = "-", Category = "Mastercard", Amount = 900.00m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-21), Description = "Expense", Vendor = "Squeaky Kleen Car Wash", Category = "Mastercard", Amount = 19.99m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-27), Description = "Expense", Vendor = "Hicks Hardware", Category = "Mastercard", Amount = 42.40m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-28), Description = "Expense", Vendor = "Squeaky Kleen Car Wash", Category = "Mastercard", Amount = 19.99m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-28), Description = "Bought lunch for crew 102", Vendor = "Bob's Burger Joint", Category = "Mastercard", Amount = 18.97m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-30), Description = "Expense", Vendor = "Tania's Nursery", Category = "Checking", Amount = 23.50m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-32), Description = "Expense", Vendor = "Pam Seitz", Category = "Checking", Amount = 75.00m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-32), Description = "Expense", Vendor = "Chin's Gas and Oil", Category = "Mastercard", Amount = 52.56m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-34), Description = "Expense", Vendor = "Hicks Hardware", Category = "Checking", Amount = 228.75m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-35), Description = "Expense", Vendor = "Chin's Gas and Oil", Category = "Checking", Amount = 63.15m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-35), Description = "Expense", Vendor = "Tania's Nursery", Category = "Checking", Amount = 46.98m, Provider = "QBO" }
        };
    }
}

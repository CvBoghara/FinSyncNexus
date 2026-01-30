using FinSyncNexus.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace FinSyncNexus.Controllers;

public class PaymentsController : Controller
{
    public IActionResult Index(string? provider)
    {
        var data = BuildDummyPayments();

        if (!string.IsNullOrWhiteSpace(provider) && provider != "All")
        {
            data = data.Where(p => p.Provider == provider).ToList();
        }

        ViewBag.Provider = provider ?? "All";
        return View(data);
    }

    private List<PaymentItem> BuildDummyPayments()
    {
        return new List<PaymentItem>
        {
            new() { Date = DateTime.Today.AddDays(-33), Customer = "0969 Ocean View Road", Amount = 387.00m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-33), Customer = "Cool Cars", Amount = 1675.52m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-34), Customer = "Amy's Bird Sanctuary", Amount = 220.00m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-34), Customer = "Travis Waldron", Amount = 81.00m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-35), Customer = "Amy's Bird Sanctuary", Amount = 108.00m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-35), Customer = "55 Twin Lane", Amount = 50.00m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-36), Customer = "John Melton", Amount = 300.00m, Provider = "QBO" },
            new() { Date = DateTime.Today.AddDays(-37), Customer = "Sushi by Katsuyuki", Amount = 80.00m, Provider = "QBO" }
        };
    }
}

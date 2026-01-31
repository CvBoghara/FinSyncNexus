using FinSyncNexus.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace FinSyncNexus.Controllers;

public class PaymentsController : Controller
{
    private readonly AppDbContext _db;

    public PaymentsController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? provider)
    {
        var query = _db.Payments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(provider) && provider != "All")
        {
            query = query.Where(p => p.Provider == provider);
        }

        var data = await query
            .OrderByDescending(p => p.Date)
            .ToListAsync();

        ViewBag.Provider = provider ?? "All";
        return View(data);
    }
}

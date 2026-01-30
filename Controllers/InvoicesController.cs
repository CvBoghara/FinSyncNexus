using FinSyncNexus.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinSyncNexus.Controllers;

public class InvoicesController : Controller
{
    private readonly AppDbContext _db;

    public InvoicesController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? provider)
    {
        var query = _db.Invoices.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(provider) && provider != "All")
        {
            query = query.Where(i => i.Provider == provider);
        }

        var invoices = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        ViewBag.Provider = provider ?? "All";
        return View(invoices);
    }
}

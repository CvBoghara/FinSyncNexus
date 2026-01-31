using FinSyncNexus.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace FinSyncNexus.Controllers;

public class ExpensesController : Controller
{
    private readonly AppDbContext _db;

    public ExpensesController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? provider)
    {
        var query = _db.Expenses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(provider) && provider != "All")
        {
            query = query.Where(e => e.Provider == provider);
        }

        var data = await query
            .OrderByDescending(e => e.Date)
            .ToListAsync();

        ViewBag.Provider = provider ?? "All";
        return View(data);
    }
}

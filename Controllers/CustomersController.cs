using FinSyncNexus.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinSyncNexus.Controllers;

public class CustomersController : Controller
{
    private readonly AppDbContext _db;

    public CustomersController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? provider)
    {
        var query = _db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(provider) && provider != "All")
        {
            query = query.Where(c => c.Provider == provider);
        }

        var customers = await query
            .OrderBy(c => c.Name)
            .ToListAsync();

        ViewBag.Provider = provider ?? "All";
        return View(customers);
    }
}

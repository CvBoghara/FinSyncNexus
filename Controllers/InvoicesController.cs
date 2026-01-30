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

    public async Task<IActionResult> Index()
    {
        var invoices = await _db.Invoices
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return View(invoices);
    }
}

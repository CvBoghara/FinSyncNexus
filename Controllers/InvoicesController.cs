using FinSyncNexus.Data;
using FinSyncNexus.Helpers;
using FinSyncNexus.ViewModels;
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

    public async Task<IActionResult> Index(FilterViewModel filters)
    {
        filters = FilterEngine.Normalize(filters);
        var query = FilterEngine.ApplyInvoiceFilters(_db.Invoices.AsNoTracking(), filters);

        var invoices = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        ViewBag.Provider = filters.Provider;
        ViewBag.Filters = filters;
        return View(invoices);
    }
}

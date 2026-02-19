using FinSyncNexus.Data;
using FinSyncNexus.Helpers;
using FinSyncNexus.ViewModels;
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

    public async Task<IActionResult> Index(FilterViewModel filters)
    {
        filters = FilterEngine.Normalize(filters);
        var query = FilterEngine.ApplyPaymentFilters(_db.Payments.AsNoTracking(), filters);

        var data = await query
            .OrderByDescending(p => p.Date)
            .ToListAsync();

        ViewBag.Provider = filters.Provider;
        ViewBag.Filters = filters;
        return View(data);
    }
}

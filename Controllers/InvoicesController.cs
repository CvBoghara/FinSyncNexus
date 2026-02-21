using FinSyncNexus.Data;
using FinSyncNexus.Helpers;
using FinSyncNexus.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FinSyncNexus.Controllers;

[Authorize]
public class InvoicesController : Controller
{
    private readonly AppDbContext _db;

    public InvoicesController(AppDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index(FilterViewModel filters)
    {
        var userId = GetCurrentUserId();
        filters = FilterEngine.Normalize(filters);

        var query = FilterEngine.ApplyInvoiceFilters(
            _db.Invoices.AsNoTracking().Where(i => i.UserId == userId), filters);

        var invoices = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        ViewBag.Provider = filters.Provider;
        ViewBag.Filters = filters;
        return View(invoices);
    }
}

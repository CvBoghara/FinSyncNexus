using FinSyncNexus.Data;
using FinSyncNexus.Helpers;
using FinSyncNexus.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinSyncNexus.Controllers;

[Authorize]
public class PaymentsController : Controller
{
    private readonly AppDbContext _db;

    public PaymentsController(AppDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index(FilterViewModel filters)
    {
        var userId = GetCurrentUserId();
        filters = FilterEngine.Normalize(filters);

        var query = FilterEngine.ApplyPaymentFilters(
            _db.Payments.AsNoTracking().Where(p => p.UserId == userId), filters);

        var data = await query
            .OrderByDescending(p => p.Date)
            .ToListAsync();

        ViewBag.Provider = filters.Provider;
        ViewBag.Filters = filters;
        return View(data);
    }
}

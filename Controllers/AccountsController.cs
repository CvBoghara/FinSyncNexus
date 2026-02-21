using FinSyncNexus.Data;
using FinSyncNexus.Helpers;
using FinSyncNexus.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FinSyncNexus.Controllers;

[Authorize]
public class AccountsController : Controller
{
    private readonly AppDbContext _db;

    public AccountsController(AppDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index(FilterViewModel filters)
    {
        var userId = GetCurrentUserId();
        filters = FilterEngine.Normalize(filters);

        var query = FilterEngine.ApplyAccountFilters(
            _db.Accounts.AsNoTracking().Where(a => a.UserId == userId), filters);

        var accounts = await query
            .OrderBy(a => a.Name)
            .ToListAsync();

        ViewBag.Provider = filters.Provider;
        ViewBag.Filters = filters;
        return View(accounts);
    }
}

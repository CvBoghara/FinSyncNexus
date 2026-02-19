using FinSyncNexus.Data;
using FinSyncNexus.Helpers;
using FinSyncNexus.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinSyncNexus.Controllers;

public class AccountsController : Controller
{
    private readonly AppDbContext _db;

    public AccountsController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(FilterViewModel filters)
    {
        filters = FilterEngine.Normalize(filters);
        var query = FilterEngine.ApplyAccountFilters(_db.Accounts.AsNoTracking(), filters);

        var accounts = await query
            .OrderBy(a => a.Name)
            .ToListAsync();

        ViewBag.Provider = filters.Provider;
        ViewBag.Filters = filters;
        return View(accounts);
    }
}

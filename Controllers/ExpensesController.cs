using FinSyncNexus.Data;
using FinSyncNexus.Helpers;
using FinSyncNexus.ViewModels;
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

    public async Task<IActionResult> Index(FilterViewModel filters)
    {
        filters = FilterEngine.Normalize(filters);
        var query = FilterEngine.ApplyExpenseFilters(_db.Expenses.AsNoTracking(), filters);

        var data = await query
            .OrderByDescending(e => e.Date)
            .ToListAsync();

        ViewBag.Provider = filters.Provider;
        ViewBag.Filters = filters;
        return View(data);
    }
}

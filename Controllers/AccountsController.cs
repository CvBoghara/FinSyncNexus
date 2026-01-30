using FinSyncNexus.Data;
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

    public async Task<IActionResult> Index(string? provider)
    {
        var query = _db.Accounts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(provider) && provider != "All")
        {
            query = query.Where(a => a.Provider == provider);
        }

        var accounts = await query
            .OrderBy(a => a.Name)
            .ToListAsync();

        ViewBag.Provider = provider ?? "All";
        return View(accounts);
    }
}

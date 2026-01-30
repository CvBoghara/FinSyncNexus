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

    public async Task<IActionResult> Index()
    {
        var accounts = await _db.Accounts
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync();

        return View(accounts);
    }
}

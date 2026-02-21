using FinSyncNexus.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FinSyncNexus.Controllers;

[Authorize]
public class CustomersController : Controller
{
    private readonly AppDbContext _db;

    public CustomersController(AppDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index(string? provider)
    {
        var userId = GetCurrentUserId();
        var query = _db.Customers.AsNoTracking().Where(c => c.UserId == userId);

        if (!string.IsNullOrWhiteSpace(provider) && provider != "All")
        {
            query = query.Where(c => c.Provider == provider);
        }

        var customers = await query
            .OrderBy(c => c.Name)
            .ToListAsync();

        ViewBag.Provider = provider ?? "All";
        return View(customers);
    }
}

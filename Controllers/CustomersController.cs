using FinSyncNexus.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinSyncNexus.Controllers;

public class CustomersController : Controller
{
    private readonly AppDbContext _db;

    public CustomersController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var customers = await _db.Customers
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        return View(customers);
    }
}

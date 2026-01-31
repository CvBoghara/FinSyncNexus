using FinSyncNexus.Data;
using FinSyncNexus.Services;
using FinSyncNexus.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinSyncNexus.Controllers;

public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    private readonly SyncService _syncService;

    public DashboardController(AppDbContext db, SyncService syncService)
    {
        _db = db;
        _syncService = syncService;
    }

    public async Task<IActionResult> Index()
    {
        var connections = await _db.Connections.AsNoTracking().ToListAsync();

        var invoices = await _db.Invoices.AsNoTracking().ToListAsync();
        var customers = await _db.Customers.AsNoTracking().ToListAsync();

        var viewModel = new DashboardViewModel
        {
            TotalRevenue = invoices.Sum(i => i.Amount),
            TotalInvoices = invoices.Count,
            TotalCustomers = customers.Count,
            OutstandingAmount = invoices.Where(i => i.Status == "OPEN").Sum(i => i.Amount),
            RecentInvoices = invoices
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .ToList(),
            Connections = connections
        };

        return View(viewModel);
    }
}

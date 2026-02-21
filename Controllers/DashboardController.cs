using FinSyncNexus.Data;
using FinSyncNexus.Helpers;
using FinSyncNexus.Services;
using FinSyncNexus.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FinSyncNexus.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    private readonly SyncService _syncService;

    public DashboardController(AppDbContext db, SyncService syncService)
    {
        _db = db;
        _syncService = syncService;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index(FilterViewModel filters)
    {
        var userId = GetCurrentUserId();
        filters = FilterEngine.Normalize(filters);

        var connections = await _db.Connections.AsNoTracking()
            .Where(c => c.UserId == userId)
            .ToListAsync();

        var invoiceQuery = FilterEngine.ApplyInvoiceFilters(
            _db.Invoices.AsNoTracking().Where(i => i.UserId == userId), filters);
        var invoices = await invoiceQuery.ToListAsync();

        var customerQuery = _db.Customers.AsNoTracking().Where(c => c.UserId == userId);
        if (!string.IsNullOrWhiteSpace(filters.Provider) && filters.Provider != "All")
        {
            customerQuery = customerQuery.Where(c => c.Provider == filters.Provider);
        }

        if (!string.IsNullOrWhiteSpace(filters.CustomerVendor))
        {
            customerQuery = customerQuery.Where(c => c.Name.Contains(filters.CustomerVendor));
        }

        var customers = await customerQuery.ToListAsync();

        var viewModel = new DashboardViewModel
        {
            Filters = filters,
            TotalRevenue = invoices.Sum(i => i.Amount),
            TotalInvoices = invoices.Count,
            TotalCustomers = customers.Count,
            OutstandingAmount = invoices
                .Where(i => i.Status.Equals("OPEN", StringComparison.OrdinalIgnoreCase) || 
                            i.Status.Equals("AUTHORISED", StringComparison.OrdinalIgnoreCase))
                .Sum(i => i.Amount),
            RecentInvoices = invoices
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .ToList(),
            Connections = connections
        };

        return View(viewModel);
    }
}

using FinSyncNexus.Data;
using FinSyncNexus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinSyncNexus.Controllers;

public class SyncController : Controller
{
    private readonly AppDbContext _db;
    private readonly SyncService _syncService;

    public SyncController(AppDbContext db, SyncService syncService)
    {
        _db = db;
        _syncService = syncService;
    }

    [HttpPost]
    public async Task<IActionResult> SyncNow()
    {
        var connections = await _db.Connections.Where(c => c.IsConnected).ToListAsync();

        if (!connections.Any())
        {
            TempData["Message"] = "No connected providers to sync.";
            return RedirectToAction("Index", "Dashboard");
        }

        // For college demo: keep read-only and use dummy data unless real API is enabled.
        foreach (var connection in connections)
        {
            await _syncService.SyncProviderAsync(connection);
            await _syncService.MarkSyncedAsync(connection.Provider);
        }

        TempData["Message"] = _syncService.UseRealApi()
            ? "Real data sync completed."
            : "Dummy data sync completed.";

        return RedirectToAction("Index", "Dashboard");
    }
}

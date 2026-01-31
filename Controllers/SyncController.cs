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

        var syncFailures = new List<string>();
        foreach (var connection in connections)
        {
            var synced = await _syncService.SyncProviderAsync(connection);
            if (synced)
            {
                await _syncService.MarkSyncedAsync(connection.Provider);
            }
            else
            {
                syncFailures.Add(connection.Provider);
            }
        }

        TempData["Message"] = syncFailures.Any()
            ? $"Sync failed for: {string.Join(", ", syncFailures)}. Check tokens and connection."
            : "Real data sync completed.";

        return RedirectToAction("Index", "Dashboard");
    }
}

using FinSyncNexus.Data;
using FinSyncNexus.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FinSyncNexus.Controllers;

[Authorize]
public class SyncController : Controller
{
    private readonly AppDbContext _db;
    private readonly SyncService _syncService;

    public SyncController(AppDbContext db, SyncService syncService)
    {
        _db = db;
        _syncService = syncService;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> SyncNow()
    {
        var userId = GetCurrentUserId();
        var connections = await _db.Connections
            .Where(c => c.UserId == userId && c.IsConnected)
            .ToListAsync();

        if (!connections.Any())
        {
            TempData["Message"] = "No connected providers to sync.";
            return RedirectToAction("Index", "Dashboard");
        }

        var syncFailures = new List<string>();
        foreach (var connection in connections)
        {
            var synced = await _syncService.SyncProviderAsync(connection, userId);
            if (synced)
            {
                await _syncService.MarkSyncedAsync(connection.Provider, userId);
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

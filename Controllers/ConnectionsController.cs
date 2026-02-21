using FinSyncNexus.Data;
using FinSyncNexus.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FinSyncNexus.Controllers;

[Authorize]
public class ConnectionsController : Controller
{
    private readonly AppDbContext _db;

    public ConnectionsController(AppDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        var connections = await _db.Connections.AsNoTracking()
            .Where(c => c.UserId == userId)
            .ToListAsync();

        if (connections.All(c => c.Provider != "Xero"))
        {
            connections.Add(new ConnectionStatus { UserId = userId, Provider = "Xero", IsConnected = false });
        }

        if (connections.All(c => c.Provider != "QBO"))
        {
            connections.Add(new ConnectionStatus { UserId = userId, Provider = "QBO", IsConnected = false });
        }

        return View(connections.OrderBy(c => c.Provider).ToList());
    }
}

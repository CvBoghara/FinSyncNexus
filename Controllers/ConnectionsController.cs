using FinSyncNexus.Data;
using FinSyncNexus.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinSyncNexus.Controllers;

public class ConnectionsController : Controller
{
    private readonly AppDbContext _db;

    public ConnectionsController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var connections = await _db.Connections.AsNoTracking().ToListAsync();

        if (connections.All(c => c.Provider != "Xero"))
        {
            connections.Add(new ConnectionStatus { Provider = "Xero", IsConnected = false });
        }

        if (connections.All(c => c.Provider != "QBO"))
        {
            connections.Add(new ConnectionStatus { Provider = "QBO", IsConnected = false });
        }

        return View(connections.OrderBy(c => c.Provider).ToList());
    }
}

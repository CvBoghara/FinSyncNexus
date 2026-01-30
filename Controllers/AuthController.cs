using FinSyncNexus.Data;
using FinSyncNexus.Models;
using FinSyncNexus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinSyncNexus.Controllers;

[Route("auth")]
public class AuthController : Controller
{
    private readonly AppDbContext _db;
    private readonly XeroOAuthService _xeroOAuthService;
    private readonly QboOAuthService _qboOAuthService;
    private readonly SyncService _syncService;

    public AuthController(
        AppDbContext db,
        XeroOAuthService xeroOAuthService,
        QboOAuthService qboOAuthService,
        SyncService syncService)
    {
        _db = db;
        _xeroOAuthService = xeroOAuthService;
        _qboOAuthService = qboOAuthService;
        _syncService = syncService;
    }

    [HttpGet("xero/connect")]
    public IActionResult XeroConnect()
    {
        var state = Guid.NewGuid().ToString("N");
        TempData["xero_state"] = state;

        var url = _xeroOAuthService.BuildAuthorizeUrl(state);
        return Redirect(url);
    }

    [HttpGet("xero/callback")]
    public async Task<IActionResult> XeroCallback(string code, string state)
    {
        var expectedState = TempData["xero_state"]?.ToString();
        if (string.IsNullOrWhiteSpace(code) || expectedState != state)
        {
            TempData["Message"] = "Xero connection failed. Please try again.";
            return RedirectToAction("Index", "Connections");
        }

        var (accessToken, refreshToken, tenantId) = await _xeroOAuthService.ExchangeCodeAsync(code);

        var connection = await _db.Connections.FirstOrDefaultAsync(c => c.Provider == "Xero")
                         ?? new ConnectionStatus { Provider = "Xero" };

        connection.IsConnected = true;
        connection.ConnectedAt = DateTime.UtcNow;
        connection.AccessToken = accessToken;
        connection.RefreshToken = refreshToken;
        connection.TenantId = tenantId;

        if (connection.Id == 0)
        {
            _db.Connections.Add(connection);
        }

        await _db.SaveChangesAsync();
        await _syncService.EnsureDummyDataAsync("Xero");
        await _syncService.MarkSyncedAsync("Xero");

        TempData["Message"] = "Xero connection success. Dummy data loaded.";
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet("qbo/connect")]
    public IActionResult QboConnect()
    {
        var state = Guid.NewGuid().ToString("N");
        TempData["qbo_state"] = state;

        var url = _qboOAuthService.BuildAuthorizeUrl(state);
        return Redirect(url);
    }

    [HttpGet("qbo/callback")]
    public async Task<IActionResult> QboCallback(string code, string state, string realmId)
    {
        var expectedState = TempData["qbo_state"]?.ToString();
        if (string.IsNullOrWhiteSpace(code) || expectedState != state)
        {
            TempData["Message"] = "QBO connection failed. Please try again.";
            return RedirectToAction("Index", "Connections");
        }

        var (accessToken, refreshToken) = await _qboOAuthService.ExchangeCodeAsync(code);

        var connection = await _db.Connections.FirstOrDefaultAsync(c => c.Provider == "QBO")
                         ?? new ConnectionStatus { Provider = "QBO" };

        connection.IsConnected = true;
        connection.ConnectedAt = DateTime.UtcNow;
        connection.AccessToken = accessToken;
        connection.RefreshToken = refreshToken;
        connection.RealmId = realmId;

        if (connection.Id == 0)
        {
            _db.Connections.Add(connection);
        }

        await _db.SaveChangesAsync();
        await _syncService.EnsureDummyDataAsync("QBO");
        await _syncService.MarkSyncedAsync("QBO");

        TempData["Message"] = "QBO connection success. Dummy data loaded.";
        return RedirectToAction("Index", "Dashboard");
    }
}

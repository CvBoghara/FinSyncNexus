using FinSyncNexus.Data;
using FinSyncNexus.Models;
using FinSyncNexus.Options;
using FinSyncNexus.Services;
using FinSyncNexus.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace FinSyncNexus.Controllers;

[Route("auth")]
public class AuthController : Controller
{
    private readonly AppDbContext _db;
    private readonly XeroOAuthService _xeroOAuthService;
    private readonly QboOAuthService _qboOAuthService;
    private readonly SyncService _syncService;
    private readonly AuthOptions _authOptions;

    public AuthController(
        AppDbContext db,
        XeroOAuthService xeroOAuthService,
        QboOAuthService qboOAuthService,
        SyncService syncService,
        IOptions<AuthOptions> authOptions)
    {
        _db = db;
        _xeroOAuthService = xeroOAuthService;
        _qboOAuthService = qboOAuthService;
        _syncService = syncService;
        _authOptions = authOptions.Value;
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

        var token = await _xeroOAuthService.ExchangeCodeAsync(code);

        var connection = await _db.Connections.FirstOrDefaultAsync(c => c.Provider == "Xero")
                         ?? new ConnectionStatus { Provider = "Xero" };

        connection.IsConnected = true;
        connection.ConnectedAt = DateTime.UtcNow;
        connection.AccessToken = token.AccessToken;
        connection.RefreshToken = token.RefreshToken;
        connection.TenantId = token.TenantId;
        connection.AccessTokenExpiresAtUtc = token.AccessTokenExpiresAtUtc;
        connection.RefreshTokenExpiresAtUtc = token.RefreshTokenExpiresAtUtc;

        if (connection.Id == 0)
        {
            _db.Connections.Add(connection);
        }

        await _db.SaveChangesAsync();
        var synced = await _syncService.SyncProviderAsync(connection);
        if (synced)
        {
            await _syncService.MarkSyncedAsync("Xero");
            TempData["Message"] = "Xero connection success. Real data synced.";
        }
        else
        {
            TempData["Message"] = "Xero connected. Sync failed - check tokens/tenant.";
        }
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

        var token = await _qboOAuthService.ExchangeCodeAsync(code);

        var connection = await _db.Connections.FirstOrDefaultAsync(c => c.Provider == "QBO")
                         ?? new ConnectionStatus { Provider = "QBO" };

        connection.IsConnected = true;
        connection.ConnectedAt = DateTime.UtcNow;
        connection.AccessToken = token.AccessToken;
        connection.RefreshToken = token.RefreshToken;
        connection.AccessTokenExpiresAtUtc = token.AccessTokenExpiresAtUtc;
        connection.RefreshTokenExpiresAtUtc = token.RefreshTokenExpiresAtUtc;
        connection.RealmId = realmId;

        if (connection.Id == 0)
        {
            _db.Connections.Add(connection);
        }

        await _db.SaveChangesAsync();
        var synced = await _syncService.SyncProviderAsync(connection);
        if (synced)
        {
            await _syncService.MarkSyncedAsync("QBO");
            TempData["Message"] = "QBO connection success. Real data synced.";
        }
        else
        {
            TempData["Message"] = "QBO connected. Sync failed - check tokens/company.";
        }
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl)
    {
        var model = new LoginViewModel
        {
            ReturnUrl = returnUrl
        };
        return View(model);
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Username == _authOptions.Username && model.Password == _authOptions.Password)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, _authOptions.DisplayName),
                new(ClaimTypes.NameIdentifier, model.Username)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Dashboard");
        }

        model.ErrorMessage = "Invalid username or password.";
        return View(model);
    }

    [HttpGet("logout")]
    public IActionResult Logout()
    {
        return View();
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutConfirmed()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Auth");
    }
}

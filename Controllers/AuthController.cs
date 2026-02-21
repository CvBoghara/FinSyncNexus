using FinSyncNexus.Data;
using FinSyncNexus.Helpers;
using FinSyncNexus.Models;
using FinSyncNexus.Services;
using FinSyncNexus.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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

    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    // ─── Registration ────────────────────────────────────────────────

    [HttpGet("register")]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var emailLower = model.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(u => u.Email == emailLower);
        if (exists)
        {
            model.ErrorMessage = "An account with this email already exists.";
            return View(model);
        }

        var user = new AppUser
        {
            FullName = model.FullName.Trim(),
            Email = emailLower,
            PasswordHash = PasswordHelper.HashPassword(model.Password),
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await SignInUserAsync(user);

        return RedirectToAction("Index", "Dashboard", new { autoSync = true });
    }

    // ─── Login ───────────────────────────────────────────────────────

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var emailLower = model.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == emailLower);

        if (user == null || !PasswordHelper.VerifyPassword(model.Password, user.PasswordHash))
        {
            model.ErrorMessage = "Invalid email or password.";
            return View(model);
        }

        await SignInUserAsync(user);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Dashboard", new { autoSync = true });
    }

    // ─── Logout ──────────────────────────────────────────────────────

    [HttpGet("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        return View();
    }

    [HttpPost("logout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutConfirmed()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Auth");
    }

    // ─── Xero OAuth ──────────────────────────────────────────────────

    [HttpGet("xero/connect")]
    [Authorize]
    public IActionResult XeroConnect()
    {
        var state = Guid.NewGuid().ToString("N");
        TempData["xero_state"] = state;

        var url = _xeroOAuthService.BuildAuthorizeUrl(state);
        return Redirect(url);
    }

    [HttpGet("xero/callback")]
    [Authorize]
    public async Task<IActionResult> XeroCallback(string code, string state)
    {
        var expectedState = TempData["xero_state"]?.ToString();
        if (string.IsNullOrWhiteSpace(code) || expectedState != state)
        {
            TempData["Message"] = "Xero connection failed. Please try again.";
            return RedirectToAction("Index", "Connections");
        }

        var userId = GetCurrentUserId();
        var token = await _xeroOAuthService.ExchangeCodeAsync(code);

        var connection = await _db.Connections
                             .FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == "Xero")
                         ?? new ConnectionStatus { UserId = userId, Provider = "Xero" };

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
        var synced = await _syncService.SyncProviderAsync(connection, userId);
        if (synced)
        {
            await _syncService.MarkSyncedAsync("Xero", userId);
            TempData["Message"] = "Xero connection success. Real data synced.";
        }
        else
        {
            TempData["Message"] = "Xero connected. Sync failed - check tokens/tenant.";
        }
        return RedirectToAction("Index", "Dashboard");
    }

    // ─── QBO OAuth ───────────────────────────────────────────────────

    [HttpGet("qbo/connect")]
    [Authorize]
    public IActionResult QboConnect()
    {
        var state = Guid.NewGuid().ToString("N");
        TempData["qbo_state"] = state;

        var url = _qboOAuthService.BuildAuthorizeUrl(state);
        return Redirect(url);
    }

    [HttpGet("qbo/callback")]
    [Authorize]
    public async Task<IActionResult> QboCallback(string code, string state, string realmId)
    {
        var expectedState = TempData["qbo_state"]?.ToString();
        if (string.IsNullOrWhiteSpace(code) || expectedState != state)
        {
            TempData["Message"] = "QBO connection failed. Please try again.";
            return RedirectToAction("Index", "Connections");
        }

        var userId = GetCurrentUserId();
        var token = await _qboOAuthService.ExchangeCodeAsync(code);

        var connection = await _db.Connections
                             .FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == "QBO")
                         ?? new ConnectionStatus { UserId = userId, Provider = "QBO" };

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
        var synced = await _syncService.SyncProviderAsync(connection, userId);
        if (synced)
        {
            await _syncService.MarkSyncedAsync("QBO", userId);
            TempData["Message"] = "QBO connection success. Real data synced.";
        }
        else
        {
            TempData["Message"] = "QBO connected. Sync failed - check tokens/company.";
        }
        return RedirectToAction("Index", "Dashboard");
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private async Task SignInUserAsync(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });
    }
}

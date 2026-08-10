using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SQPortal.Data;
using SQPortal.Services;

namespace SQPortal.Controllers;

///////// test-only: delete this controller (and Views/Account/Login.cshtml) when AD is linked \\\\\\\\\\

/// <summary>
/// Test-environment sign-in: lists the hardcoded users from Data/TestUsers.cs
/// and signs the selected one in through the cookie, without passwords.
/// Obsolete once Windows SSO is linked (see the marked AD block in Program.cs).
/// </summary>
[AllowAnonymous]
public class AccountController : Controller
{
    private readonly AuditService _audit;
    private readonly ILogger<AccountController> _logger;

    public AccountController(AuditService audit, ILogger<AccountController> logger)
    {
        _audit = audit;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        ViewData["ReturnUrl"] = SafeReturnUrl(returnUrl);
        return View(TestUsers.All);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> LoginAs(string username, string? returnUrl = null)
    {
        // Only names on the fixed roster are accepted.
        var user = TestUsers.Find(username);
        if (user == null)
        {
            _logger.LogWarning("Rejected test sign-in for unknown user {Username} from {RemoteIp}",
                username, HttpContext.Connection.RemoteIpAddress);
            return RedirectToAction(nameof(Login));
        }

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Username),
                new Claim(ClaimTypes.Name, user.DisplayName)
            },
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        _logger.LogInformation("Test user {Username} signed in", user.Username);
        await _audit.LogAsync("Sign-in", $"{user.Username} signed in", userOverride: user.DisplayName);

        var safeReturnUrl = SafeReturnUrl(returnUrl);
        return safeReturnUrl != null
            ? Redirect(safeReturnUrl)
            : RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _audit.LogAsync("Sign-out", "Signed out");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private string? SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
}

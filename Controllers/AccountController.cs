using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SQPortal.Data;

namespace SQPortal.Controllers;

///////// test-only: delete this controller (and Views/Account/Login.cshtml) when AD is linked \\\\\\\\\\

/// <summary>
/// Test-environment sign-in. The login page lists the hardcoded users from
/// Data/TestUsers.cs; pressing one signs them in through the existing cookie —
/// no passwords. Once Windows SSO is linked (the marked AD block in Program.cs)
/// there is no login page and no sign-out, and this controller goes away.
/// </summary>
[AllowAnonymous]
public class AccountController : Controller
{
    private readonly ILogger<AccountController> _logger;

    public AccountController(ILogger<AccountController> logger)
    {
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
        // Only the fixed roster gets in — arbitrary posted names are rejected.
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

        var safeReturnUrl = SafeReturnUrl(returnUrl);
        return safeReturnUrl != null
            ? Redirect(safeReturnUrl)
            : RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private string? SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
}

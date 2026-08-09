using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SQPortal.Helpers;
using SQPortal.Models.ViewModels.Account;

namespace SQPortal.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly IConfiguration _config;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IConfiguration config, ILogger<AccountController> logger)
    {
        _config = config;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new LoginViewModel
        {
            ReturnUrl = SafeReturnUrl(returnUrl),
            NotConfigured = !CredentialVerifier.IsConfigured(_config)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        vm.ReturnUrl = SafeReturnUrl(vm.ReturnUrl);
        vm.NotConfigured = !CredentialVerifier.IsConfigured(_config);

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        if (!CredentialVerifier.Verify(_config, vm.Username, vm.Password))
        {
            _logger.LogWarning("Failed sign-in attempt for user {Username} from {RemoteIp}",
                vm.Username, HttpContext.Connection.RemoteIpAddress);
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(vm);
        }

        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, vm.Username) },
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return vm.ReturnUrl != null
            ? Redirect(vm.ReturnUrl)
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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SQPortal.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => RedirectToAction("Index", "Dashboard");

    /// <summary>Production error page — no stack traces or exception details leave the server.</summary>
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}

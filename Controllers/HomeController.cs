using Microsoft.AspNetCore.Mvc;

namespace SQPortal.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
}

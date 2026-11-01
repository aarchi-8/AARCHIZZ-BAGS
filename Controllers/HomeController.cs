using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AARCHIZZ.Models;
using AARCHIZZ.Services;

namespace AARCHIZZ.Controllers;

public class HomeController : Controller
{
    private readonly DatabaseCatalogService _catalog;

    public HomeController(DatabaseCatalogService catalog)
    {
        _catalog = catalog;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _catalog.GetHomeAsync(HttpContext.RequestAborted));
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult ShippingReturns()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

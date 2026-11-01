using AARCHIZZ.Models;
using AARCHIZZ.Services;
using Microsoft.AspNetCore.Mvc;

namespace AARCHIZZ.Controllers;

public class ShopController : Controller
{
  private readonly DatabaseCatalogService _catalog;

  public ShopController(DatabaseCatalogService catalog)
  {
    _catalog = catalog;
  }

  [HttpGet("/shop")]
  public async Task<IActionResult> Index(string? category, string? search, string sort = "featured")
  {
    return View(await _catalog.GetShopAsync(category, search, sort, HttpContext.RequestAborted));
  }

  [HttpGet("/shop/{slug}")]
  public async Task<IActionResult> Details(string slug)
  {
    var model = await _catalog.GetDetailsAsync(slug, HttpContext.RequestAborted);
    if (model is null)
    {
      Response.StatusCode = StatusCodes.Status404NotFound;
      return View("NotFound", slug);
    }

    return View(model);
  }
}
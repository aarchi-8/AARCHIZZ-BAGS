using AARCHIZZ.Models;
using AARCHIZZ.Services;
using Microsoft.AspNetCore.Mvc;

namespace AARCHIZZ.Controllers;

public class CollectionsController : Controller
{
  private readonly DatabaseCatalogService _catalog;

  public CollectionsController(DatabaseCatalogService catalog)
  {
    _catalog = catalog;
  }

  [HttpGet("/collections")]
  public async Task<IActionResult> Index()
  {
    return View(new CollectionsViewModel { Collections = await _catalog.GetCollectionsAsync(HttpContext.RequestAborted) });
  }

  [HttpGet("/collections/{slug}")]
  public async Task<IActionResult> Details(string slug)
  {
    var model = await _catalog.GetCollectionAsync(slug, HttpContext.RequestAborted);
    if (model is null)
    {
      Response.StatusCode = StatusCodes.Status404NotFound;
      return View("NotFound", slug);
    }

    return View("Details", model);
  }
}
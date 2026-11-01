using AARCHIZZ.Data;
using AARCHIZZ.Data.Entities;
using AARCHIZZ.Models;
using AARCHIZZ.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AARCHIZZ.Controllers;

[Route("Admin/Categories")]
[Authorize(Policy = "AdminOnly")]
public class AdminCategoriesController : Controller
{
  private readonly AARCHIZZDbContext _db;
  private readonly IProductImageStorage _imageStorage;

  public AdminCategoriesController(AARCHIZZDbContext db, IProductImageStorage imageStorage)
  {
    _db = db;
    _imageStorage = imageStorage;
  }

  [HttpGet("")]
  public async Task<IActionResult> Index()
  {
    var categories = await _db.Categories.AsNoTracking().Include(category => category.Products).OrderBy(category => category.Name).ToListAsync(HttpContext.RequestAborted);
    return View(new AdminCategoryListViewModel { Categories = categories });
  }

  [HttpGet("Create")]
  public IActionResult Create() => View(new AdminCategoryFormViewModel { IsPublished = true });

  [HttpPost("Create")]
  [RequestSizeLimit(6 * 1024 * 1024)]
  [ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(AdminCategoryFormViewModel model)
  {
    PrepareSlug(model);
    await ValidateSlugAsync(model.Slug);
    if (!ModelState.IsValid)
    {
      return View(model);
    }

    StoredProductImage? storedImage = null;
    try
    {
      if (model.ImageFile is not null)
        storedImage = await _imageStorage.SaveCategoryImageAsync(model.ImageFile, HttpContext.RequestAborted);

      _db.Categories.Add(new Category
      {
        Name = model.Name.Trim(),
        Slug = model.Slug.Trim().ToLowerInvariant(),
        Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
        ImageUrl = storedImage?.StorageKey,
        IsPublished = model.IsPublished
      });
      await _db.SaveChangesAsync(HttpContext.RequestAborted);
    }
    catch (InvalidDataException exception)
    {
      ModelState.AddModelError(nameof(model.ImageFile), exception.Message);
      return View(model);
    }
    catch (DbUpdateException)
    {
      if (storedImage is not null)
        await _imageStorage.DeleteAsync(storedImage.StorageKey, CancellationToken.None);
      ModelState.AddModelError(nameof(model.Slug), "That slug is already in use. Choose a different slug.");
      return View(model);
    }
    catch
    {
      if (storedImage is not null)
        await _imageStorage.DeleteAsync(storedImage.StorageKey, CancellationToken.None);
      throw;
    }
    TempData["AdminCategoryMessage"] = "Category created successfully.";
    return RedirectToAction(nameof(Index));
  }

  [HttpGet("Edit/{id:int}")]
  public async Task<IActionResult> Edit(int id)
  {
    var category = await _db.Categories.AsNoTracking().SingleOrDefaultAsync(category => category.Id == id, HttpContext.RequestAborted);
    return category is null ? NotFound() : View(ToForm(category));
  }

  [HttpPost("Edit/{id:int}")]
  [RequestSizeLimit(6 * 1024 * 1024)]
  [ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, AdminCategoryFormViewModel model)
  {
    if (id != model.Id) return BadRequest();
    var category = await _db.Categories.SingleOrDefaultAsync(category => category.Id == id, HttpContext.RequestAborted);
    if (category is null) return NotFound();
    model.CurrentImageUrl = category.ImageUrl;
    PrepareSlug(model);
    await ValidateSlugAsync(model.Slug, id);
    if (!ModelState.IsValid) return View(model);

    var previousImageUrl = category.ImageUrl;
    StoredProductImage? replacementImage = null;
    try
    {
      if (model.ImageFile is not null)
        replacementImage = await _imageStorage.SaveCategoryImageAsync(model.ImageFile, HttpContext.RequestAborted);
    }
    catch (InvalidDataException exception)
    {
      model.CurrentImageUrl = previousImageUrl;
      ModelState.AddModelError(nameof(model.ImageFile), exception.Message);
      return View(model);
    }

    category.Name = model.Name.Trim();
    category.Slug = model.Slug.Trim().ToLowerInvariant();
    category.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
    if (replacementImage is not null)
      category.ImageUrl = replacementImage.StorageKey;
    category.IsPublished = model.IsPublished;
    try
    {
      await _db.SaveChangesAsync(HttpContext.RequestAborted);
    }
    catch (DbUpdateException)
    {
      if (replacementImage is not null)
        await _imageStorage.DeleteAsync(replacementImage.StorageKey, CancellationToken.None);
      ModelState.AddModelError(nameof(model.Slug), "That slug is already in use. Choose a different slug.");
      model.CurrentImageUrl = previousImageUrl;
      return View(model);
    }
    catch
    {
      if (replacementImage is not null)
        await _imageStorage.DeleteAsync(replacementImage.StorageKey, CancellationToken.None);
      throw;
    }

    if (replacementImage is not null && !string.IsNullOrWhiteSpace(previousImageUrl) &&
        !string.Equals(previousImageUrl, replacementImage.StorageKey, StringComparison.OrdinalIgnoreCase))
    {
      var categoryStillUsesImage = await _db.Categories.AnyAsync(other => other.Id != id && other.ImageUrl == previousImageUrl, HttpContext.RequestAborted);
      var productStillUsesImage = await _db.ProductImages.AnyAsync(image => image.UrlOrStorageKey == previousImageUrl, HttpContext.RequestAborted);
      if (!categoryStillUsesImage && !productStillUsesImage)
        await _imageStorage.DeleteAsync(previousImageUrl, HttpContext.RequestAborted);
    }
    TempData["AdminCategoryMessage"] = "Category updated successfully.";
    return RedirectToAction(nameof(Index));
  }

  [HttpPost("Publish/{id:int}")]
  [ValidateAntiForgeryToken]
  public Task<IActionResult> Publish(int id) => SetPublishedAsync(id, true);

  [HttpPost("Unpublish/{id:int}")]
  [ValidateAntiForgeryToken]
  public Task<IActionResult> Unpublish(int id) => SetPublishedAsync(id, false);

  [HttpGet("Delete/{id:int}")]
  public async Task<IActionResult> Delete(int id)
  {
    var category = await _db.Categories.AsNoTracking().Include(category => category.Products).SingleOrDefaultAsync(category => category.Id == id, HttpContext.RequestAborted);
    return category is null ? NotFound() : View(new AdminCategoryDeleteViewModel { Id = id, Name = category.Name, ProductCount = category.Products.Count });
  }

  [HttpPost("Delete/{id:int}")]
  [ValidateAntiForgeryToken]
  public async Task<IActionResult> Delete(int id, bool confirmDeletion)
  {
    var category = await _db.Categories.Include(category => category.Products).SingleOrDefaultAsync(category => category.Id == id, HttpContext.RequestAborted);
    if (category is null) return NotFound();
    if (category.Products.Count > 0)
    {
      TempData["AdminCategoryError"] = "This category cannot be deleted while products are associated with it. Reassign or remove the products first.";
      return RedirectToAction(nameof(Index));
    }

    if (!confirmDeletion)
    {
      ModelState.AddModelError(string.Empty, "Confirm that you want to permanently delete this category.");
      return View(new AdminCategoryDeleteViewModel { Id = id, Name = category.Name, ProductCount = 0 });
    }

    _db.Categories.Remove(category);
    try
    {
      await _db.SaveChangesAsync(HttpContext.RequestAborted);
    }
    catch (DbUpdateException)
    {
      TempData["AdminCategoryError"] = "This category could not be deleted because products are associated with it. Reassign or remove the products first.";
      return RedirectToAction(nameof(Index));
    }
    TempData["AdminCategoryMessage"] = "Category deleted successfully.";
    return RedirectToAction(nameof(Index));
  }

  private async Task<IActionResult> SetPublishedAsync(int id, bool published)
  {
    var category = await _db.Categories.SingleOrDefaultAsync(category => category.Id == id, HttpContext.RequestAborted);
    if (category is null) return NotFound();
    category.IsPublished = published;
    await _db.SaveChangesAsync(HttpContext.RequestAborted);
    TempData["AdminCategoryMessage"] = published ? "Category published." : "Category unpublished.";
    return RedirectToAction(nameof(Index));
  }

  private async Task ValidateSlugAsync(string slug, int? excludedId = null)
  {
    if (await _db.Categories.AnyAsync(category => category.Slug == slug.Trim().ToLowerInvariant() && category.Id != excludedId, HttpContext.RequestAborted))
      ModelState.AddModelError(nameof(AdminCategoryFormViewModel.Slug), "That slug is already in use.");
  }

  private void PrepareSlug(AdminCategoryFormViewModel model)
  {
    if (string.IsNullOrWhiteSpace(model.Slug) && !string.IsNullOrWhiteSpace(model.Name))
    {
      model.Slug = string.Join('-', model.Name.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
      ModelState.Remove(nameof(model.Slug));
    }
  }

  private static AdminCategoryFormViewModel ToForm(Category category) => new()
  {
    Id = category.Id,
    Name = category.Name,
    Slug = category.Slug,
    Description = category.Description,
    CurrentImageUrl = category.ImageUrl,
    IsPublished = category.IsPublished
  };
}

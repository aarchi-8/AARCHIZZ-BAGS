using AARCHIZZ.Data;
using AARCHIZZ.Data.Entities;
using AARCHIZZ.Models;
using AARCHIZZ.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AARCHIZZ.Controllers;

[Route("Admin/Products")]
[Authorize(Policy = "AdminOnly")]
public class AdminProductsController : Controller
{
  private readonly AARCHIZZDbContext _db;
  private readonly IProductImageStorage _imageStorage;

  public AdminProductsController(AARCHIZZDbContext db, IProductImageStorage imageStorage)
  {
    _db = db;
    _imageStorage = imageStorage;
  }

  [HttpGet("")]
  public async Task<IActionResult> Index(string? search, int? categoryId, bool? published, int page = 1)
  {
    const int pageSize = 12;
    page = Math.Max(1, page);
    var query = _db.Products.AsNoTracking().Include(product => product.Category).AsQueryable();

    if (!string.IsNullOrWhiteSpace(search))
    {
      var term = search.Trim();
      query = query.Where(product => product.Name.Contains(term) || product.Sku.Contains(term) || product.Slug.Contains(term));
    }

    if (categoryId.HasValue)
    {
      query = query.Where(product => product.CategoryId == categoryId.Value);
    }

    if (published.HasValue)
    {
      query = query.Where(product => product.IsPublished == published.Value);
    }

    var totalProducts = await query.CountAsync(HttpContext.RequestAborted);
    var totalPages = Math.Max(1, (int)Math.Ceiling(totalProducts / (double)pageSize));
    page = Math.Min(page, totalPages);
    var products = await query.OrderByDescending(product => product.CreatedUtc)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(HttpContext.RequestAborted);

    return View(new AdminProductListViewModel
    {
      Products = products,
      Categories = await _db.Categories.AsNoTracking().Where(category => category.IsPublished).OrderBy(category => category.Name).ToListAsync(HttpContext.RequestAborted),
      Search = search,
      CategoryId = categoryId,
      Published = published,
      Page = page,
      PageSize = pageSize,
      TotalProducts = totalProducts
    });
  }

  [HttpGet("Create")]
  public async Task<IActionResult> Create()
  {
    await LoadCategoriesAsync();
    return View(new AdminProductCreateViewModel());
  }

  [HttpPost("Create")]
  [ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(AdminProductCreateViewModel model)
  {
    await LoadCategoriesAsync();
    model.Images ??= [];
    model.Variants ??= [];
    model.NewImageAltTexts ??= [];
    model.NewImageVariantIndexes ??= [];

    var slugInput = string.IsNullOrWhiteSpace(model.Slug) ? model.Name : model.Slug;
    var normalizedSlug = NormalizeSlug(slugInput);
    model.Slug = normalizedSlug;
    ModelState.Remove(nameof(model.Slug));
    if (string.IsNullOrWhiteSpace(normalizedSlug))
    {
      ModelState.AddModelError(nameof(model.Slug), "Enter a product name or provide a URL slug.");
    }
    else if (normalizedSlug.Length > 180)
    {
      ModelState.AddModelError(nameof(model.Slug), "Slug must be 180 characters or fewer.");
    }

    var normalizedSku = model.Sku?.Trim().ToUpperInvariant() ?? string.Empty;

    if (!string.IsNullOrWhiteSpace(normalizedSlug) &&
        await _db.Products.AnyAsync(product => product.Slug == normalizedSlug, HttpContext.RequestAborted))
    {
      ModelState.AddModelError(nameof(model.Slug), "That URL slug is already in use. Choose a different slug or product name.");
    }

    if (!string.IsNullOrWhiteSpace(normalizedSku) &&
        await _db.Products.AnyAsync(product => product.Sku == normalizedSku, HttpContext.RequestAborted))
    {
      ModelState.AddModelError(nameof(model.Sku), "That SKU is already in use.");
    }

    if (model.SalePrice.HasValue && (model.SalePrice <= 0 || model.SalePrice >= model.Price))
    {
      ModelState.AddModelError(nameof(model.SalePrice), "Sale price must be greater than zero and lower than the regular price.");
    }

    await ValidateVariantSkusAsync(model);

    if (!model.CategoryId.HasValue ||
        !await _db.Categories.AnyAsync(category => category.Id == model.CategoryId.Value && category.IsPublished, HttpContext.RequestAborted))
    {
      ModelState.Remove(nameof(model.CategoryId));
      ModelState.AddModelError(nameof(model.CategoryId), "Select a valid published category.");
    }

    if (model.Images.Count > 8)
    {
      ModelState.AddModelError(nameof(model.Images), "You can upload up to 8 images at a time.");
    }

    if (model.PrimaryNewImageIndex is < 0 || model.PrimaryNewImageIndex >= model.Images.Count)
    {
      ModelState.AddModelError(nameof(model.PrimaryNewImageIndex), "Select a valid primary image.");
    }

    if (!ModelState.IsValid)
    {
      return View(model);
    }

    var product = new Product
    {
      Name = model.Name.Trim(),
      Slug = normalizedSlug,
      Sku = normalizedSku,
      CategoryId = model.CategoryId!.Value,
      Availability = model.StockQuantity > 0 ? "In stock" : "Out of stock",
      CreatedUtc = DateTime.UtcNow
    };
    ApplyProductValues(product, model);
    ApplyVariants(product, model.Variants);

    var uploadedKeys = new List<string>();
    try
    {
      var storedImages = await StoreImagesAsync(model.Images, uploadedKeys);
      for (var index = 0; index < storedImages.Count; index++)
      {
        var variantIndex = index < model.NewImageVariantIndexes.Count ? model.NewImageVariantIndexes[index] : null;
        var selectedVariant = variantIndex.HasValue ? GetVariantAtIndex(product.Variants, model.Variants, variantIndex.Value) : null;
        product.Images.Add(new ProductImage
        {
          UrlOrStorageKey = storedImages[index].StorageKey,
          SortOrder = index,
          IsPrimary = model.PrimaryNewImageIndex == index,
          AltText = index < model.NewImageAltTexts.Count ? model.NewImageAltTexts[index] : null,
          ProductVariant = selectedVariant
        });
      }

      if (storedImages.Count > 0 && model.PrimaryNewImageIndex is null)
      {
        product.Images.First().IsPrimary = true;
      }

      _db.Products.Add(product);
      await _db.SaveChangesAsync(HttpContext.RequestAborted);
    }
    catch (InvalidDataException exception)
    {
      await DeleteStoredImagesAsync(uploadedKeys);
      ModelState.AddModelError(nameof(model.Images), exception.Message);
      return View(model);
    }
    catch (DbUpdateException)
    {
      await DeleteStoredImagesAsync(uploadedKeys);
      ModelState.AddModelError(string.Empty, "The product could not be saved. The slug or SKU may already be in use.");
      return View(model);
    }

    TempData["AdminProductMessage"] = "Product created successfully.";
    return RedirectToAction(nameof(Index));
  }

  [HttpGet("Edit/{id:int}")]
  public async Task<IActionResult> Edit(int id)
  {
    var product = await _db.Products.AsNoTracking().Include(product => product.Images).Include(product => product.Variants).SingleOrDefaultAsync(product => product.Id == id, HttpContext.RequestAborted);
    if (product is null)
    {
      return NotFound();
    }

    await LoadCategoriesAsync(product.CategoryId);
    return View(new AdminProductEditViewModel
    {
      Id = product.Id,
      Name = product.Name,
      Slug = product.Slug,
      Sku = product.Sku,
      Description = product.Description,
      ShortDescription = product.ShortDescription,
      Price = product.Price,
      SalePrice = product.SalePrice,
      StockQuantity = product.StockQuantity,
      LowStockThreshold = product.LowStockThreshold,
      Barcode = product.Barcode,
      CategoryId = product.CategoryId,
      Material = product.Material,
      Brand = product.Brand, StyleCode = product.StyleCode, ProductType = product.ProductType, Pattern = product.Pattern, Finish = product.Finish, Tags = product.Tags,
      LiningMaterial = product.LiningMaterial, Color = product.Color,
      Length = product.Length, Width = product.Width, Height = product.Height, DimensionUnit = product.DimensionUnit,
      Weight = product.Weight, WeightUnit = product.WeightUnit, ClosureType = product.ClosureType, HandleStrapType = product.HandleStrapType,
      CompartmentCount = product.CompartmentCount, PocketCount = product.PocketCount, Occasion = product.Occasion, CareInstructions = product.CareInstructions,
      PackageContents = product.PackageContents, ShippingInformation = product.ShippingInformation, ShippingPolicyReference = product.ShippingPolicyReference,
      ReturnInformation = product.ReturnInformation, ReturnPolicyReference = product.ReturnPolicyReference, WarrantyInformation = product.WarrantyInformation,
      SeoTitle = product.SeoTitle, SeoMetaDescription = product.SeoMetaDescription,
      Dimensions = product.Dimensions,
      IsPublished = product.IsPublished,
      IsFeatured = product.IsFeatured,
      Variants = product.Variants.OrderBy(variant => variant.Id).Select(variant => new AdminProductVariantViewModel { Id = variant.Id, Name = variant.Name, Color = variant.Color, Size = variant.Size, Sku = variant.Sku, PriceOverride = variant.PriceOverride, StockQuantity = variant.StockQuantity, IsAvailable = variant.IsAvailable }).ToList(),
      ExistingImages = product.Images.OrderBy(image => image.SortOrder).Select(image => new AdminProductImageViewModel
      {
        Id = image.Id,
        UrlOrStorageKey = image.UrlOrStorageKey,
        AltText = image.AltText,
        SortOrder = image.SortOrder,
        IsPrimary = image.IsPrimary,
        ProductVariantId = image.ProductVariantId
      }).ToList()
    });
  }

  [HttpPost("Edit/{id:int}")]
  [ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, AdminProductEditViewModel model)
  {
    if (id != model.Id)
    {
      return BadRequest();
    }

    var product = await _db.Products.Include(product => product.Images).Include(product => product.Variants).SingleOrDefaultAsync(product => product.Id == id, HttpContext.RequestAborted);
    if (product is null)
    {
      return NotFound();
    }

    await LoadCategoriesAsync(product.CategoryId);
    model.Images ??= [];
    model.Variants ??= [];
    model.ExistingImages ??= [];
    model.NewImageAltTexts ??= [];
    model.NewImageVariantIndexes ??= [];
    await ValidateProductAsync(model, id, product.CategoryId);
    await ValidateVariantSkusAsync(model, id);
    if (model.Images.Count > 8 || model.ExistingImages.Count + model.Images.Count > 8)
    {
      ModelState.AddModelError(nameof(model.Images), "A product can have up to 8 images.");
    }
    if (model.PrimaryNewImageIndex is < 0 || model.PrimaryNewImageIndex >= model.Images.Count)
    {
      ModelState.AddModelError(nameof(model.PrimaryNewImageIndex), "Select a valid primary image.");
    }
    if (!ModelState.IsValid)
    {
      return View(model);
    }

    var uploadedKeys = new List<string>();
    try
    {
      var storedImages = await StoreImagesAsync(model.Images, uploadedKeys);
      var removedImages = product.Images
          .Where(image => model.ExistingImages.Any(posted => posted.Id == image.Id && posted.Remove))
          .ToList();
      var retainedImages = product.Images
          .Where(image => !removedImages.Contains(image))
          .OrderBy(image => model.ExistingImages.FirstOrDefault(posted => posted.Id == image.Id)?.SortOrder ?? image.SortOrder)
          .ToList();

      foreach (var image in removedImages)
      {
        _db.ProductImages.Remove(image);
      }

      var nextSortOrder = 0;
      foreach (var image in retainedImages)
      {
        image.SortOrder = nextSortOrder++;
        image.IsPrimary = model.PrimaryExistingImageId == image.Id;
        image.AltText = model.ExistingImages.FirstOrDefault(posted => posted.Id == image.Id)?.AltText;
        var selectedVariantId = model.ExistingImages.FirstOrDefault(posted => posted.Id == image.Id)?.ProductVariantId;
        image.ProductVariantId = selectedVariantId.HasValue && product.Variants.Any(variant => variant.Id == selectedVariantId.Value) ? selectedVariantId : null;
      }

      for (var index = 0; index < storedImages.Count; index++)
      {
        var variantIndex = index < model.NewImageVariantIndexes.Count ? model.NewImageVariantIndexes[index] : null;
        var selectedVariant = variantIndex.HasValue ? GetVariantAtIndex(product.Variants, model.Variants, variantIndex.Value) : null;
        product.Images.Add(new ProductImage
        {
          UrlOrStorageKey = storedImages[index].StorageKey,
          SortOrder = nextSortOrder++,
          IsPrimary = model.PrimaryNewImageIndex == index,
          AltText = index < model.NewImageAltTexts.Count ? model.NewImageAltTexts[index] : null,
          ProductVariant = selectedVariant
        });
      }

      if (product.Images.Any() && !product.Images.Any(image => image.IsPrimary && !removedImages.Contains(image)))
      {
        product.Images.OrderBy(image => image.SortOrder).First().IsPrimary = true;
      }

      ApplyProductValues(product, model);
      product.UpdatedUtc = DateTime.UtcNow;
      await _db.SaveChangesAsync(HttpContext.RequestAborted);

      foreach (var removedImage in removedImages)
      {
        await _imageStorage.DeleteAsync(removedImage.UrlOrStorageKey, HttpContext.RequestAborted);
      }
    }
    catch (InvalidDataException exception)
    {
      await DeleteStoredImagesAsync(uploadedKeys);
      ModelState.AddModelError(nameof(model.Images), exception.Message);
      return View(model);
    }
    catch (DbUpdateException)
    {
      await DeleteStoredImagesAsync(uploadedKeys);
      ModelState.AddModelError(string.Empty, "The product could not be saved. The slug or SKU may already be in use.");
      return View(model);
    }

    TempData["AdminProductMessage"] = "Product updated successfully.";
    return RedirectToAction(nameof(Index));
  }

  [HttpGet("Delete/{id:int}")]
  public async Task<IActionResult> Delete(int id)
  {
    var product = await _db.Products.AsNoTracking().SingleOrDefaultAsync(product => product.Id == id, HttpContext.RequestAborted);
    if (product is null)
    {
      return NotFound();
    }

    return View(new AdminProductDeleteViewModel { Id = product.Id, Name = product.Name, Sku = product.Sku });
  }

  [HttpPost("Delete/{id:int}")]
  [ValidateAntiForgeryToken]
  public async Task<IActionResult> DeleteConfirmed(int id)
  {
    var product = await _db.Products
        .Include(product => product.Images)
        .Include(product => product.Variants)
        .SingleOrDefaultAsync(product => product.Id == id, HttpContext.RequestAborted);
    if (product is null)
    {
      return NotFound();
    }

    var imageStorageKeys = product.Images.Select(image => image.UrlOrStorageKey).ToList();
    _db.Products.Remove(product);
    await _db.SaveChangesAsync(HttpContext.RequestAborted);
    foreach (var storageKey in imageStorageKeys)
    {
      await _imageStorage.DeleteAsync(storageKey, HttpContext.RequestAborted);
    }

    TempData["AdminProductMessage"] = "Product deleted successfully.";
    return RedirectToAction(nameof(Index));
  }

  [HttpPost("Publish/{id:int}")]
  [ValidateAntiForgeryToken]
  public Task<IActionResult> Publish(int id) => SetPublishedAsync(id, true);

  [HttpPost("Unpublish/{id:int}")]
  [ValidateAntiForgeryToken]
  public Task<IActionResult> Unpublish(int id) => SetPublishedAsync(id, false);

  private async Task<IActionResult> SetPublishedAsync(int id, bool published)
  {
    var product = await _db.Products.SingleOrDefaultAsync(product => product.Id == id, HttpContext.RequestAborted);
    if (product is null)
    {
      return NotFound();
    }

    product.IsPublished = published;
    product.UpdatedUtc = DateTime.UtcNow;
    await _db.SaveChangesAsync(HttpContext.RequestAborted);
    TempData["AdminProductMessage"] = published ? "Product published." : "Product unpublished.";
    return RedirectToAction(nameof(Index));
  }

  private async Task ValidateProductAsync(AdminProductCreateViewModel model, int? excludedId = null, int? currentCategoryId = null)
  {
    var normalizedSlug = NormalizeSlug(model.Slug);
    model.Slug = normalizedSlug;
    ModelState.Remove(nameof(model.Slug));
    if (string.IsNullOrWhiteSpace(normalizedSlug))
    {
      ModelState.AddModelError(nameof(model.Slug), "Enter a URL slug.");
    }
    else if (normalizedSlug.Length > 180)
    {
      ModelState.AddModelError(nameof(model.Slug), "Slug must be 180 characters or fewer.");
    }

    var normalizedSku = model.Sku?.Trim().ToUpperInvariant() ?? string.Empty;
    if (await _db.Products.AnyAsync(product => product.Slug == normalizedSlug && product.Id != excludedId, HttpContext.RequestAborted))
    {
      ModelState.AddModelError(nameof(model.Slug), "That slug is already in use.");
    }

    if (await _db.Products.AnyAsync(product => product.Sku == normalizedSku && product.Id != excludedId, HttpContext.RequestAborted))
    {
      ModelState.AddModelError(nameof(model.Sku), "That SKU is already in use.");
    }

    if (model.SalePrice.HasValue && (model.SalePrice <= 0 || model.SalePrice >= model.Price))
    {
      ModelState.AddModelError(nameof(model.SalePrice), "Sale price must be greater than zero and lower than the regular price.");
    }

    if (!model.CategoryId.HasValue || !await _db.Categories.AnyAsync(category => category.Id == model.CategoryId.Value && (category.IsPublished || category.Id == currentCategoryId), HttpContext.RequestAborted))
    {
      ModelState.AddModelError(nameof(model.CategoryId), "Select a valid published category.");
    }
  }

  private static void ApplyProductValues(Product product, AdminProductCreateViewModel model)
  {
    product.Name = model.Name?.Trim() ?? string.Empty;
    product.Slug = NormalizeSlug(model.Slug);
    product.Sku = model.Sku?.Trim().ToUpperInvariant() ?? string.Empty;
    product.Description = model.Description?.Trim() ?? string.Empty;
    product.Price = model.Price;
    product.SalePrice = model.SalePrice;
    product.StockQuantity = model.StockQuantity;
    product.LowStockThreshold = model.LowStockThreshold;
    product.Barcode = Clean(model.Barcode);
    product.CategoryId = model.CategoryId!.Value;
    product.ShortDescription = Clean(model.ShortDescription);
    product.Brand = Clean(model.Brand);
    product.StyleCode = Clean(model.StyleCode);
    product.ProductType = Clean(model.ProductType);
    product.Pattern = Clean(model.Pattern);
    product.Finish = Clean(model.Finish);
    product.Tags = Clean(model.Tags);
    product.Material = string.IsNullOrWhiteSpace(model.Material) ? null : model.Material.Trim();
    product.Dimensions = string.IsNullOrWhiteSpace(model.Dimensions) ? null : model.Dimensions.Trim();
    product.LiningMaterial = Clean(model.LiningMaterial); product.Color = Clean(model.Color);
    product.Length = model.Length; product.Width = model.Width; product.Height = model.Height; product.DimensionUnit = Clean(model.DimensionUnit);
    product.Weight = model.Weight; product.WeightUnit = Clean(model.WeightUnit);
    product.ClosureType = Clean(model.ClosureType); product.HandleStrapType = Clean(model.HandleStrapType);
    product.CompartmentCount = model.CompartmentCount; product.PocketCount = model.PocketCount; product.Occasion = Clean(model.Occasion); product.CareInstructions = Clean(model.CareInstructions);
    product.PackageContents = Clean(model.PackageContents); product.ShippingInformation = Clean(model.ShippingInformation); product.ShippingPolicyReference = Clean(model.ShippingPolicyReference);
    product.ReturnInformation = Clean(model.ReturnInformation); product.ReturnPolicyReference = Clean(model.ReturnPolicyReference); product.WarrantyInformation = Clean(model.WarrantyInformation);
    product.SeoTitle = Clean(model.SeoTitle); product.SeoMetaDescription = Clean(model.SeoMetaDescription);
    product.IsPublished = model.IsPublished;
    product.IsFeatured = model.IsFeatured;
    product.Availability = model.StockQuantity > 0 ? "In stock" : "Out of stock";
    product.CurrencyCode = "INR";
  }

  private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

  private async Task ValidateVariantSkusAsync(AdminProductCreateViewModel model, int? productId = null)
  {
    var skus = (model.Variants ?? []).Select(variant => variant.Sku?.Trim().ToUpperInvariant()).Where(sku => !string.IsNullOrWhiteSpace(sku)).ToList();
    foreach (var duplicate in skus.GroupBy(sku => sku, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key))
      ModelState.AddModelError(nameof(model.Variants), $"Variant SKU '{duplicate}' is repeated.");
    if (skus.Count > 0 && await _db.ProductVariants.AnyAsync(variant => variant.ProductId != productId && skus.Contains(variant.Sku!), HttpContext.RequestAborted))
      ModelState.AddModelError(nameof(model.Variants), "A variant SKU is already used by this product.");
  }

  private static void ApplyVariants(Product product, IEnumerable<AdminProductVariantViewModel> posted)
  {
    var items = posted.Where(item => !string.IsNullOrWhiteSpace(item.Color) || !string.IsNullOrWhiteSpace(item.Size) || !string.IsNullOrWhiteSpace(item.Name) || !string.IsNullOrWhiteSpace(item.Sku)).ToList();
    foreach (var existing in product.Variants.Where(item => items.All(postedItem => postedItem.Id != item.Id)).ToList())
    {
      foreach (var image in product.Images.Where(image => image.ProductVariantId == existing.Id))
      {
        image.ProductVariantId = null;
        image.ProductVariant = null;
      }
      product.Variants.Remove(existing);
    }
    foreach (var item in items)
    {
      var variant = item.Id.HasValue ? product.Variants.FirstOrDefault(existing => existing.Id == item.Id.Value) : null;
      if (variant is null) { variant = new ProductVariant(); product.Variants.Add(variant); }
      variant.Color = Clean(item.Color); variant.Size = Clean(item.Size); variant.Sku = Clean(item.Sku)?.ToUpperInvariant();
      variant.Name = Clean(item.Name) ?? string.Join(" / ", new[] { variant.Color, variant.Size }.Where(value => !string.IsNullOrWhiteSpace(value)));
      variant.PriceOverride = item.PriceOverride; variant.StockQuantity = item.StockQuantity; variant.IsAvailable = item.IsAvailable;
    }
  }

  private static ProductVariant? GetVariantAtIndex(IEnumerable<ProductVariant> persisted, IReadOnlyList<AdminProductVariantViewModel> posted, int index)
  {
    var entered = posted.Where(item => !string.IsNullOrWhiteSpace(item.Color) || !string.IsNullOrWhiteSpace(item.Size) || !string.IsNullOrWhiteSpace(item.Name) || !string.IsNullOrWhiteSpace(item.Sku)).ToList();
    if (index < 0 || index >= entered.Count) return null;
    var viewModel = entered[index];
    return viewModel.Id.HasValue ? persisted.FirstOrDefault(variant => variant.Id == viewModel.Id.Value) : persisted.Where(variant => variant.Id == 0).ElementAtOrDefault(index - entered.Take(index).Count(item => item.Id.HasValue));
  }

  private async Task LoadCategoriesAsync(int? currentCategoryId = null)
  {
    ViewBag.Categories = await _db.Categories.AsNoTracking()
      .Where(category => category.IsPublished || (currentCategoryId.HasValue && category.Id == currentCategoryId.Value))
        .OrderBy(category => category.Name)
        .ToListAsync(HttpContext.RequestAborted);
  }

  private static string NormalizeSlug(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
      return string.Empty;

    var slug = new System.Text.StringBuilder(value.Length);
    foreach (var character in value.Trim().ToLowerInvariant())
    {
      if (char.IsLetterOrDigit(character))
      {
        slug.Append(character);
      }
      else if (slug.Length > 0 && slug[^1] != '-')
      {
        slug.Append('-');
      }
    }

    return slug.ToString().Trim('-');
  }

  private async Task<List<StoredProductImage>> StoreImagesAsync(IReadOnlyList<Microsoft.AspNetCore.Http.IFormFile> files, List<string> storedKeys)
  {
    var stored = new List<StoredProductImage>();
    foreach (var file in files)
    {
      var image = await _imageStorage.SaveAsync(file, HttpContext.RequestAborted);
      stored.Add(image);
      storedKeys.Add(image.StorageKey);
    }

    return stored;
  }

  private async Task DeleteStoredImagesAsync(IEnumerable<string> storageKeys)
  {
    foreach (var storageKey in storageKeys)
    {
      await _imageStorage.DeleteAsync(storageKey, CancellationToken.None);
    }
  }
}

using System.Text.Json;
using AARCHIZZ.Data;
using AARCHIZZ.Data.Entities;
using AARCHIZZ.Models;
using Microsoft.EntityFrameworkCore;

namespace AARCHIZZ.Services;

public sealed class ShoppingCartSessionStore
{
  private const string SessionKey = "AARCHIZZ.ShoppingCart.v1";
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  public List<ShoppingCartLine> Read(ISession session)
  {
    var json = session.GetString(SessionKey);
    if (string.IsNullOrWhiteSpace(json)) return [];

    try
    {
      return JsonSerializer.Deserialize<List<ShoppingCartLine>>(json, JsonOptions)?
        .Where(line => line.ProductId > 0 && line.Quantity > 0 && line.Quantity <= 999)
        .GroupBy(line => new { line.ProductId, line.VariantId })
        .Select(group => new ShoppingCartLine
        {
          ProductId = group.Key.ProductId,
          VariantId = group.Key.VariantId,
          Quantity = Math.Min(999, group.Sum(line => line.Quantity))
        }).ToList() ?? [];
    }
    catch (JsonException)
    {
      session.Remove(SessionKey);
      return [];
    }
  }

  public void Write(ISession session, IReadOnlyCollection<ShoppingCartLine> lines)
  {
    if (lines.Count == 0)
    {
      session.Remove(SessionKey);
      return;
    }

    session.SetString(SessionKey, JsonSerializer.Serialize(lines, JsonOptions));
  }

  public int GetCount(ISession session) => Read(session).Sum(line => line.Quantity);
}

public sealed class ShoppingCartLine
{
  public int ProductId { get; set; }
  public int? VariantId { get; set; }
  public int Quantity { get; set; }
}

public sealed class ShoppingCartService
{
  private readonly AARCHIZZDbContext _db;
  private readonly ShoppingCartSessionStore _sessionStore;

  public ShoppingCartService(AARCHIZZDbContext db, ShoppingCartSessionStore sessionStore)
  {
    _db = db;
    _sessionStore = sessionStore;
  }

  public async Task<ShoppingCartViewModel> GetAsync(ISession session, CancellationToken cancellationToken)
  {
    var lines = _sessionStore.Read(session);
    if (lines.Count == 0) return new ShoppingCartViewModel();

    var productIds = lines.Select(line => line.ProductId).Distinct().ToArray();
    var products = await _db.Products.AsNoTracking()
      .AsSplitQuery()
      .Include(product => product.Category)
      .Include(product => product.Images)
      .Include(product => product.Variants)
      .Where(product => productIds.Contains(product.Id))
      .ToDictionaryAsync(product => product.Id, cancellationToken);

    var items = new List<ShoppingCartItemViewModel>();
    foreach (var line in lines)
    {
      if (!products.TryGetValue(line.ProductId, out var product))
      {
        items.Add(new ShoppingCartItemViewModel
        {
          ProductId = line.ProductId,
          VariantId = line.VariantId,
          Name = "Unavailable item",
          ImageUrl = DatabaseCatalogService.PlaceholderImageUrl,
          OrderImageUrl = null,
          Quantity = line.Quantity,
          IsAvailable = false,
          CanAdjustQuantity = false,
          AvailabilityMessage = "This product is no longer available. Remove it from your bag."
        });
        continue;
      }
      var variant = line.VariantId.HasValue
        ? product.Variants.FirstOrDefault(candidate => candidate.Id == line.VariantId.Value)
        : null;
      var variantRequired = product.Variants.Count > 0;
      var validVariant = !variantRequired && line.VariantId is null || variant is not null;
      var stock = validVariant ? InventoryAvailability.GetAvailableQuantity(product, variant) : 0;
      var publicProduct = product.IsPublished && product.Category.IsPublished;
      var unitPrice = variant?.PriceOverride is > 0 ? variant.PriceOverride.Value : product.Price;
      if (product.SalePrice is > 0 and var salePrice && salePrice < product.Price && salePrice < unitPrice)
      {
        unitPrice = salePrice;
      }

      var variantImages = product.Images.Where(image => image.ProductVariantId == line.VariantId)
        .OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder);
      var imageUrl = line.VariantId.HasValue
        ? variantImages.Select(image => image.UrlOrStorageKey).FirstOrDefault(url => !string.IsNullOrWhiteSpace(url))
        : null;
      imageUrl ??= product.Images.Where(image => image.ProductVariantId is null)
        .OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder)
        .Select(image => image.UrlOrStorageKey).FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));

      var labelParts = variant is null
        ? []
        : new[] { variant.Color, variant.Size }.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
      var isAvailable = publicProduct && validVariant && stock > 0 && line.Quantity <= stock;
      items.Add(new ShoppingCartItemViewModel
      {
        ProductId = product.Id,
        VariantId = line.VariantId,
        Name = product.Name,
        Slug = product.Slug,
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? DatabaseCatalogService.PlaceholderImageUrl : imageUrl,
        OrderImageUrl = imageUrl,
        VariantLabel = labelParts.Length > 0 ? string.Join(" · ", labelParts) : variant?.Name,
        UnitPrice = unitPrice,
        LineTotal = unitPrice * line.Quantity,
        CurrencyCode = string.IsNullOrWhiteSpace(product.CurrencyCode) ? "INR" : product.CurrencyCode,
        Quantity = line.Quantity,
        AvailableQuantity = stock,
        IsAvailable = isAvailable,
        CanAdjustQuantity = publicProduct && validVariant && stock > 0,
        AvailabilityMessage = !publicProduct ? "This item is no longer available. Remove it from your bag."
          : !validVariant ? "Choose a valid product option or remove this item."
          : stock <= 0 ? "This item is out of stock. Remove it from your bag."
          : line.Quantity > stock ? $"Only {stock} currently available. Reduce your quantity."
          : null
      });
    }

    var currencies = items.Select(item => item.CurrencyCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    var subtotal = currencies.Count <= 1
      ? items.Sum(item => item.LineTotal)
      : 0m;
    return new ShoppingCartViewModel
    {
      Items = items,
      Subtotal = subtotal,
      CurrencyCode = currencies.Count == 1 ? currencies[0] : currencies.Count == 0 ? "INR" : string.Empty,
      ItemCount = lines.Sum(line => line.Quantity)
    };
  }

  public async Task<string?> AddAsync(ISession session, int productId, int? variantId, int quantity, CancellationToken cancellationToken)
  {
    if (quantity <= 0 || quantity > 999) return "Choose a quantity from 1 to 999.";

    var product = await _db.Products.AsNoTracking().Include(item => item.Category).Include(item => item.Variants)
      .SingleOrDefaultAsync(item => item.Id == productId, cancellationToken);
    var error = ValidateSelection(product, variantId, quantity);
    if (error is not null) return error;

    var lines = _sessionStore.Read(session);
    var existing = lines.FirstOrDefault(line => line.ProductId == productId && line.VariantId == variantId);
    var newQuantity = quantity + (existing?.Quantity ?? 0);
    error = ValidateSelection(product, variantId, newQuantity);
    if (error is not null) return error;

    if (existing is null) lines.Add(new ShoppingCartLine { ProductId = productId, VariantId = variantId, Quantity = quantity });
    else existing.Quantity = newQuantity;
    _sessionStore.Write(session, lines);
    return null;
  }

  public async Task<(WhatsAppOrderLine? Item, string? Error)> GetOrderItemAsync(int productId, int? variantId, int quantity, CancellationToken cancellationToken)
  {
    var product = await _db.Products.AsNoTracking().Include(item => item.Category).Include(item => item.Variants).Include(item => item.Images)
      .SingleOrDefaultAsync(item => item.Id == productId, cancellationToken);
    var error = ValidateSelection(product, variantId, quantity);
    if (error is not null) return (null, error);

    var variant = variantId.HasValue ? product!.Variants.Single(item => item.Id == variantId.Value) : null;
    var unitPrice = variant?.PriceOverride is > 0 ? variant.PriceOverride.Value : product!.Price;
    if (product!.SalePrice is > 0 and var salePrice && salePrice < product.Price && salePrice < unitPrice) unitPrice = salePrice;
    var labels = variant is null ? [] : new[] { variant.Color, variant.Size }
      .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()).ToArray();
    var label = labels.Length > 0 ? string.Join(" · ", labels) : variant?.Name;
    var currency = string.IsNullOrWhiteSpace(product.CurrencyCode) ? "INR" : product.CurrencyCode.Trim().ToUpperInvariant();
    var selectedImage = variantId.HasValue
      ? product.Images.Where(image => image.ProductVariantId == variantId.Value).OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder).Select(image => image.UrlOrStorageKey).FirstOrDefault()
      : null;
    selectedImage ??= product.Images.Where(image => image.ProductVariantId is null).OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder).Select(image => image.UrlOrStorageKey).FirstOrDefault();
    return (new WhatsAppOrderLine(product.Name, product.Slug, label, quantity, unitPrice, currency, selectedImage), null);
  }

  public async Task<string?> UpdateAsync(ISession session, int productId, int? variantId, int quantity, CancellationToken cancellationToken)
  {
    if (quantity <= 0 || quantity > 999) return "Choose a quantity from 1 to 999. Use Remove to delete this item.";
    var lines = _sessionStore.Read(session);
    var existing = lines.FirstOrDefault(line => line.ProductId == productId && line.VariantId == variantId);
    if (existing is null) return "That item is no longer in your bag. Refresh the page and try again.";

    var product = await _db.Products.AsNoTracking().Include(item => item.Category).Include(item => item.Variants)
      .SingleOrDefaultAsync(item => item.Id == productId, cancellationToken);
    var error = ValidateSelection(product, variantId, quantity);
    // A line can become overstocked after it was added. Let the customer step it
    // downward one at a time while still requiring the product/option to be valid.
    if (error is not null && quantity < existing.Quantity && ValidateSelection(product, variantId, 1) is null)
    {
      error = null;
    }
    if (error is not null) return error;

    existing.Quantity = quantity;
    _sessionStore.Write(session, lines);
    return null;
  }

  public void Remove(ISession session, int productId, int? variantId)
  {
    var lines = _sessionStore.Read(session);
    lines.RemoveAll(line => line.ProductId == productId && line.VariantId == variantId);
    _sessionStore.Write(session, lines);
  }

  private static string? ValidateSelection(Product? product, int? variantId, int quantity)
  {
    if (product is null || !product.IsPublished || !product.Category.IsPublished)
      return "This product is no longer available.";
    if (quantity <= 0 || quantity > 999) return "Choose a quantity from 1 to 999.";

    var variantRequired = product.Variants.Count > 0;
    if (variantRequired)
    {
      var variant = variantId.HasValue ? product.Variants.FirstOrDefault(item => item.Id == variantId.Value) : null;
      if (variant is null) return "Choose a valid product option before adding it to your bag.";
      var availableQuantity = InventoryAvailability.GetAvailableQuantity(product, variant);
      if (availableQuantity < 1) return "That product option is currently unavailable.";
      if (quantity > availableQuantity) return $"Only {availableQuantity} of that option are currently available.";
    }
    else
    {
      if (variantId.HasValue) return "That product option is no longer available.";
      if (product.StockQuantity < 1) return "This product is currently out of stock.";
      if (quantity > product.StockQuantity) return $"Only {product.StockQuantity} are currently available.";
    }

    return null;
  }
}

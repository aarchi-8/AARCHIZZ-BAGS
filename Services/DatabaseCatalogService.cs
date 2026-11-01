using AARCHIZZ.Data;
using AARCHIZZ.Data.Entities;
using AARCHIZZ.Models;
using Microsoft.EntityFrameworkCore;

namespace AARCHIZZ.Services;

public class DatabaseCatalogService
{
  public const string PlaceholderImageUrl = "/images/product-placeholder.svg";
  public const string CategoryPlaceholderImageUrl = "/images/category-placeholder.svg";
  private readonly AARCHIZZDbContext _db;

  public DatabaseCatalogService(AARCHIZZDbContext db)
  {
    _db = db;
  }

  public async Task<HomeViewModel> GetHomeAsync(CancellationToken cancellationToken = default)
  {
    var categories = await _db.Categories.AsNoTracking()
        .Where(category => category.IsPublished)
        .OrderBy(category => category.Name)
        .Take(6)
        .ToListAsync(cancellationToken);
    var products = await PublishedProducts()
        .OrderByDescending(product => product.CreatedUtc)
        .ToListAsync(cancellationToken);
    var featured = products.Where(product => product.IsFeatured).Take(4).ToList();
    if (featured.Count == 0)
    {
      featured = products.Take(4).ToList();
    }

    return new HomeViewModel
    {
      Categories = categories.Select(ToCategoryCard).ToList(),
      NewArrivals = products.Take(4).Select(ToCard).ToList(),
      FeaturedProducts = featured.Select(ToCard).ToList()
    };
  }

  public async Task<ShopViewModel> GetShopAsync(string? category, string? search, string sort, CancellationToken cancellationToken = default)
  {
    var categories = await PublishedCategories().OrderBy(category => category.Name).ToListAsync(cancellationToken);
    var query = PublishedProducts();
    var selectedCategory = categories.FirstOrDefault(item =>
        item.Name.Equals(category, StringComparison.OrdinalIgnoreCase) ||
        item.Slug.Equals(category, StringComparison.OrdinalIgnoreCase));

    if (selectedCategory is not null)
    {
      query = query.Where(product => product.CategoryId == selectedCategory.Id);
    }

    if (!string.IsNullOrWhiteSpace(search))
    {
      var term = search.Trim();
      var pattern = $"%{term.ToLowerInvariant()}%";
      query = query.Where(product =>
        EF.Functions.Like(product.Name.ToLower(), pattern) ||
        EF.Functions.Like(product.Brand!.ToLower(), pattern) ||
        EF.Functions.Like(product.Category.Name.ToLower(), pattern) ||
        EF.Functions.Like(product.ShortDescription!.ToLower(), pattern) ||
        EF.Functions.Like(product.Description.ToLower(), pattern) ||
        EF.Functions.Like(product.StyleCode!.ToLower(), pattern) ||
        EF.Functions.Like(product.ProductType!.ToLower(), pattern) ||
        EF.Functions.Like(product.Color!.ToLower(), pattern) ||
        EF.Functions.Like(product.Tags!.ToLower(), pattern) ||
        EF.Functions.Like(product.Sku.ToLower(), pattern));
    }

    query = sort.ToLowerInvariant() switch
    {
      "price-asc" => query.OrderBy(product => product.SalePrice ?? product.Price),
      "price-desc" => query.OrderByDescending(product => product.SalePrice ?? product.Price),
      "name" => query.OrderBy(product => product.Name),
      "featured" => query.OrderByDescending(product => product.IsFeatured).ThenByDescending(product => product.CreatedUtc),
      _ => query.OrderByDescending(product => product.CreatedUtc)
    };

    var totalProducts = await query.CountAsync(cancellationToken);
    var products = await query.ToListAsync(cancellationToken);
    var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
    return new ShopViewModel
    {
      Products = products.Select(ToCatalogProduct).ToList(),
      Categories = categories.Select(category => category.Name).ToList(),
      SelectedCategory = selectedCategory?.Name,
      Search = normalizedSearch,
      Sort = sort,
      TotalProducts = totalProducts
    };
  }

  public async Task<ProductDetailsViewModel?> GetDetailsAsync(string slug, CancellationToken cancellationToken = default)
  {
    var product = await PublishedProducts().SingleOrDefaultAsync(product => product.Slug == slug, cancellationToken);
    return product is null ? null : new ProductDetailsViewModel { Product = ToCatalogProduct(product) };
  }

  public async Task<CatalogProduct?> GetPublishedProductByIdAsync(int id, CancellationToken cancellationToken = default)
  {
    var product = await PublishedProducts().SingleOrDefaultAsync(product => product.Id == id, cancellationToken);
    return product is null ? null : ToCatalogProduct(product);
  }

  public async Task<IReadOnlyList<CatalogProduct>> GetPublishedProductsByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
  {
    var productIds = ids.Distinct().ToArray();
    if (productIds.Length == 0)
    {
      return [];
    }

    var products = await PublishedProducts()
      .Where(product => productIds.Contains(product.Id))
      .ToListAsync(cancellationToken);
    var order = productIds.Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index);
    return products.OrderBy(product => order[product.Id]).Select(ToCatalogProduct).ToList();
  }

  public async Task<IReadOnlyList<CollectionCard>> GetCollectionsAsync(CancellationToken cancellationToken = default)
  {
    return await PublishedCategories().OrderBy(category => category.Name).Select(category => ToCollectionCard(category)).ToListAsync(cancellationToken);
  }

  public async Task<ShopViewModel?> GetCollectionAsync(string slug, CancellationToken cancellationToken = default)
  {
    if (slug.Equals("new-arrivals", StringComparison.OrdinalIgnoreCase))
    {
      var products = await PublishedProducts()
        .Where(product => product.StockQuantity > 0)
        .OrderByDescending(product => product.CreatedUtc)
        .Take(12)
        .ToListAsync(cancellationToken);
      return ToCollectionViewModel("New Arrivals", products);
    }

    var category = await PublishedCategories().SingleOrDefaultAsync(category => category.Slug == slug, cancellationToken);
    if (category is null)
    {
      return null;
    }

    var categoryProducts = await PublishedProducts().Where(product => product.CategoryId == category.Id).OrderByDescending(product => product.CreatedUtc).ToListAsync(cancellationToken);
    return ToCollectionViewModel(category.Name, categoryProducts);
  }

  private IQueryable<Product> PublishedProducts() => _db.Products.AsNoTracking()
    .AsSplitQuery()
      .Include(product => product.Category)
      .Include(product => product.Images)
      .Include(product => product.Variants)
      .Where(product => product.IsPublished && product.Category.IsPublished);

  private IQueryable<Category> PublishedCategories() => _db.Categories.AsNoTracking().Where(category => category.IsPublished);

  private static ShopViewModel ToCollectionViewModel(string title, IReadOnlyList<Product> products) => new()
  {
    Products = products.Select(ToCatalogProduct).ToList(),
    SelectedCategory = title,
    TotalProducts = products.Count,
    Sort = "featured"
  };

  private static CategoryCard ToCategoryCard(Category category) => new()
  {
    Name = category.Name,
    Description = category.Description ?? "Explore the edit",
    ImageUrl = string.IsNullOrWhiteSpace(category.ImageUrl) ? CategoryPlaceholderImageUrl : category.ImageUrl,
    Slug = category.Slug
  };

  private static CollectionCard ToCollectionCard(Category category) => new()
  {
    Name = category.Name,
    Slug = category.Slug,
    Description = category.Description ?? "Explore the edit",
    ImageUrl = string.IsNullOrWhiteSpace(category.ImageUrl) ? CategoryPlaceholderImageUrl : category.ImageUrl
  };

  private static ProductCard ToCard(Product product) => new()
  {
    Id = product.Id,
    Name = product.Name,
    Price = $"\u20B9{product.Price:N2}",
    SalePrice = product.SalePrice is > 0 and var salePrice && salePrice < product.Price ? $"\u20B9{salePrice:N2}" : null,
    DiscountLabel = product.SalePrice is > 0 and var discountSale && discountSale < product.Price
      ? $"{(1m - discountSale / product.Price) * 100m:0}% off"
      : null,
    ImageUrl = PrimaryImage(product),
    Label = product.IsFeatured ? "Featured" : null,
    Slug = product.Slug,
    ShortDescription = string.IsNullOrWhiteSpace(product.ShortDescription) ? product.Description : product.ShortDescription,
    Colors = product.Variants.Where(variant => InventoryAvailability.GetAvailableQuantity(product, variant) > 0 && !string.IsNullOrWhiteSpace(variant.Color))
      .Select(variant => variant.Color!).Distinct().ToList(),
    AvailableVariants = product.Variants.Where(variant => InventoryAvailability.GetAvailableQuantity(product, variant) > 0)
      .Select(variant => new CartVariantOption
      {
        Id = variant.Id,
        Label = string.Join(" · ", new[] { variant.Color, variant.Size }.Where(value => !string.IsNullOrWhiteSpace(value))) is { Length: > 0 } label
          ? label
          : variant.Name
      }).ToList(),
    IsAvailable = InventoryAvailability.IsAvailable(product)
  };

  private static CatalogProduct ToCatalogProduct(Product product) => new()
  {
    Id = product.Id,
    Slug = product.Slug,
    Name = product.Name,
    Category = product.Category.Name,
    Price = product.Price,
    CurrencyCode = product.CurrencyCode,
    SalePrice = product.SalePrice is > 0 and var salePrice && salePrice < product.Price ? salePrice : null,
    ImageUrl = PrimaryImage(product),
    Gallery = product.Images.OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder).Select(image => image.UrlOrStorageKey).Where(key => !string.IsNullOrWhiteSpace(key)).ToList(),
    GalleryImages = product.Images.OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder).Where(image => !string.IsNullOrWhiteSpace(image.UrlOrStorageKey)).Select(image => new CatalogImage { Url = image.UrlOrStorageKey, AltText = image.AltText }).ToList(),
    Label = product.IsFeatured ? "Featured" : null,
    Description = product.Description,
    ShortDescription = product.ShortDescription,
    Features = [],
    Material = product.Material,
    Dimensions = product.Dimensions,
    LiningMaterial = product.LiningMaterial, Color = product.Color,
    Length = product.Length, Width = product.Width, Height = product.Height, DimensionUnit = product.DimensionUnit,
    Weight = product.Weight, WeightUnit = product.WeightUnit,
    ClosureType = product.ClosureType, HandleStrapType = product.HandleStrapType,
    CompartmentCount = product.CompartmentCount, PocketCount = product.PocketCount, Occasion = product.Occasion, CareInstructions = product.CareInstructions,
    PackageContents = product.PackageContents, ShippingInformation = product.ShippingInformation, ShippingPolicyReference = product.ShippingPolicyReference,
    ReturnInformation = product.ReturnInformation, ReturnPolicyReference = product.ReturnPolicyReference, WarrantyInformation = product.WarrantyInformation,
    SeoTitle = product.SeoTitle, SeoMetaDescription = product.SeoMetaDescription, StockQuantity = product.StockQuantity,
    Brand = product.Brand, StyleCode = product.StyleCode, ProductType = product.ProductType,
    Pattern = product.Pattern, Finish = product.Finish, Tags = product.Tags,
    Variants = product.Variants.OrderBy(variant => variant.Id).Select(variant => new CatalogVariant { Id = variant.Id, Name = variant.Name, Color = variant.Color, Size = variant.Size, PriceOverride = variant.PriceOverride, StockQuantity = variant.StockQuantity, IsAvailable = variant.IsAvailable, Images = variant.Images.OrderBy(image => image.SortOrder).Select(image => new CatalogImage { Url = image.UrlOrStorageKey, AltText = image.AltText }).ToList() }).ToList(),
    Colors = product.Variants.Where(variant => InventoryAvailability.GetAvailableQuantity(product, variant) > 0)
      .Select(variant => variant.Color)
      .Append(product.Color)
      .Where(color => !string.IsNullOrWhiteSpace(color))
      .Select(color => color!)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToList(),
    Sku = product.Sku,
    Availability = product.Availability
  };

  private static string PrimaryImage(Product product)
  {
    var image = product.Images.OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder).Select(image => image.UrlOrStorageKey).FirstOrDefault(key => !string.IsNullOrWhiteSpace(key));
    return string.IsNullOrWhiteSpace(image) ? PlaceholderImageUrl : image;
  }
}

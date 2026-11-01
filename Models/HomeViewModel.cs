namespace AARCHIZZ.Models;

public class HomeViewModel
{
  public IReadOnlyList<CategoryCard> Categories { get; init; } = [];
  public IReadOnlyList<ProductCard> NewArrivals { get; init; } = [];
  public IReadOnlyList<ProductCard> FeaturedProducts { get; init; } = [];
}

public class CategoryCard
{
  public required string Name { get; init; }
  public required string Description { get; init; }
  public required string ImageUrl { get; init; }
  public required string Slug { get; init; }
}

public class ProductCard
{
  public int Id { get; init; }
  public required string Name { get; init; }
  public required string Price { get; init; }
  public string? SalePrice { get; init; }
  public string? DiscountLabel { get; init; }
  public required string ImageUrl { get; init; }
  public string? Label { get; init; }
  public required string Slug { get; init; }
  public string? ShortDescription { get; init; }
  public IReadOnlyList<string> Colors { get; init; } = [];
  public IReadOnlyList<CartVariantOption> AvailableVariants { get; init; } = [];
  public bool IsAvailable { get; init; }
}

public class CatalogProduct
{
  public int Id { get; init; }
  public required string Slug { get; init; }
  public required string Name { get; init; }
  public required string Category { get; init; }
  public required decimal Price { get; init; }
  public string CurrencyCode { get; init; } = "INR";
  public decimal? SalePrice { get; init; }
  public required string ImageUrl { get; init; }
  public IReadOnlyList<string> Gallery { get; init; } = [];
  public IReadOnlyList<CatalogImage> GalleryImages { get; init; } = [];
  public string? Label { get; init; }
  public required string Description { get; init; }
  public string? ShortDescription { get; init; }
  public IReadOnlyList<string> Features { get; init; } = [];
  public string? Material { get; init; }
  public string? Dimensions { get; init; }
  public string? LiningMaterial { get; init; }
  public string? Color { get; init; }
  public decimal? Length { get; init; }
  public decimal? Width { get; init; }
  public decimal? Height { get; init; }
  public string? DimensionUnit { get; init; }
  public decimal? Weight { get; init; }
  public string? WeightUnit { get; init; }
  public string? ClosureType { get; init; }
  public string? HandleStrapType { get; init; }
  public int? CompartmentCount { get; init; }
  public int? PocketCount { get; init; }
  public string? Occasion { get; init; }
  public string? CareInstructions { get; init; }
  public string? PackageContents { get; init; }
  public string? ShippingInformation { get; init; }
  public string? ShippingPolicyReference { get; init; }
  public string? ReturnInformation { get; init; }
  public string? ReturnPolicyReference { get; init; }
  public string? WarrantyInformation { get; init; }
  public string? SeoTitle { get; init; }
  public string? SeoMetaDescription { get; init; }
  public int StockQuantity { get; init; }
  public string? Brand { get; init; }
  public string? StyleCode { get; init; }
  public string? ProductType { get; init; }
  public string? Pattern { get; init; }
  public string? Finish { get; init; }
  public string? Tags { get; init; }
  public IReadOnlyList<CatalogVariant> Variants { get; init; } = [];
  public IReadOnlyList<string> Colors { get; init; } = [];
  public string? Sku { get; init; }
  public string Availability { get; init; } = "In stock";
}

public class CartVariantOption
{
  public int Id { get; init; }
  public string Label { get; init; } = string.Empty;
}

public class CatalogImage
{
  public string Url { get; init; } = string.Empty;
  public string? AltText { get; init; }
}

public class CatalogVariant
{
  public int Id { get; init; }
  public string Name { get; init; } = string.Empty;
  public string? Color { get; init; }
  public string? Size { get; init; }
  public decimal? PriceOverride { get; init; }
  public int StockQuantity { get; init; }
  public bool IsAvailable { get; init; }
  public IReadOnlyList<CatalogImage> Images { get; init; } = [];
}

public class ShopViewModel
{
  public IReadOnlyList<CatalogProduct> Products { get; init; } = [];
  public IReadOnlyList<string> Categories { get; init; } = [];
  public string? SelectedCategory { get; init; }
  public string? Search { get; init; }
  public string Sort { get; init; } = "featured";
  public int TotalProducts { get; init; }
}

public class ProductDetailsViewModel
{
  public required CatalogProduct Product { get; init; }
}

public class CollectionCard
{
  public required string Name { get; init; }
  public required string Slug { get; init; }
  public required string Description { get; init; }
  public required string ImageUrl { get; init; }
}

public class CollectionsViewModel
{
  public IReadOnlyList<CollectionCard> Collections { get; init; } = [];
}

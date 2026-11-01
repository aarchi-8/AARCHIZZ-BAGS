using System.ComponentModel.DataAnnotations;

namespace AARCHIZZ.Data.Entities;

public class Product
{
  public int Id { get; set; }

  [Required, MaxLength(180)]
  public string Name { get; set; } = string.Empty;

  [Required, MaxLength(180)]
  public string Slug { get; set; } = string.Empty;

  [Required, MaxLength(4000)]
  public string Description { get; set; } = string.Empty;

  [MaxLength(500)] public string? ShortDescription { get; set; }
  [MaxLength(120)] public string? Brand { get; set; }
  [MaxLength(120)] public string? StyleCode { get; set; }
  [MaxLength(120)] public string? ProductType { get; set; }
  [MaxLength(120)] public string? Pattern { get; set; }
  [MaxLength(120)] public string? Finish { get; set; }
  [MaxLength(1000)] public string? Tags { get; set; }

  [Required, MaxLength(80)]
  public string Sku { get; set; } = string.Empty;

  [Range(0, 999999999.99)]
  public decimal Price { get; set; }

  [Range(0, 999999999.99)]
  public decimal? SalePrice { get; set; }

  [Required, MaxLength(3)] public string CurrencyCode { get; set; } = "INR";
  public int LowStockThreshold { get; set; } = 5;
  [MaxLength(80)] public string? Barcode { get; set; }

  [MaxLength(120)]
  public string? Material { get; set; }

  [MaxLength(120)]
  public string? Dimensions { get; set; }

  [MaxLength(120)] public string? LiningMaterial { get; set; }
  [MaxLength(80)] public string? Color { get; set; }
  public decimal? Length { get; set; }
  public decimal? Width { get; set; }
  public decimal? Height { get; set; }
  [MaxLength(20)] public string? DimensionUnit { get; set; }
  public decimal? Weight { get; set; }
  [MaxLength(20)] public string? WeightUnit { get; set; }
  [MaxLength(120)] public string? ClosureType { get; set; }
  [MaxLength(120)] public string? HandleStrapType { get; set; }
  public int? CompartmentCount { get; set; }
  public int? PocketCount { get; set; }
  [MaxLength(500)] public string? Occasion { get; set; }
  [MaxLength(2000)] public string? CareInstructions { get; set; }
  [MaxLength(2000)] public string? PackageContents { get; set; }
  [MaxLength(2000)] public string? ShippingInformation { get; set; }
  [MaxLength(200)] public string? ShippingPolicyReference { get; set; }
  [MaxLength(2000)] public string? ReturnInformation { get; set; }
  [MaxLength(200)] public string? ReturnPolicyReference { get; set; }
  [MaxLength(2000)] public string? WarrantyInformation { get; set; }
  [MaxLength(180)] public string? SeoTitle { get; set; }
  [MaxLength(320)] public string? SeoMetaDescription { get; set; }

  [MaxLength(80)]
  public string Availability { get; set; } = "In stock";

  public int StockQuantity { get; set; }
  public bool IsPublished { get; set; }
  public bool IsFeatured { get; set; }
  public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
  public DateTime? UpdatedUtc { get; set; }

  public int CategoryId { get; set; }
  public Category Category { get; set; } = null!;
  public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
  public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}

using System.ComponentModel.DataAnnotations;
using AARCHIZZ.Data.Entities;
using Microsoft.AspNetCore.Http;

namespace AARCHIZZ.Models;

public class AdminProductListViewModel
{
  public IReadOnlyList<Product> Products { get; init; } = [];
  public IReadOnlyList<Category> Categories { get; init; } = [];
  public string? Search { get; init; }
  public int? CategoryId { get; init; }
  public bool? Published { get; init; }
  public int Page { get; init; } = 1;
  public int PageSize { get; init; } = 12;
  public int TotalProducts { get; init; }
  public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalProducts / (double)PageSize));
}

public class AdminProductCreateViewModel
{
  [Required(ErrorMessage = "Enter a product name."), StringLength(180)] public string Name { get; set; } = string.Empty;
  [StringLength(180)] [Display(Name = "URL slug")] public string? Slug { get; set; }
  [Required(ErrorMessage = "Enter a product SKU."), StringLength(80)] public string Sku { get; set; } = string.Empty;
  [Required(ErrorMessage = "Enter a product description."), StringLength(4000)] public string Description { get; set; } = string.Empty;
  [StringLength(500)] public string? ShortDescription { get; set; }
  [Range(typeof(decimal), "0", "999999999.99", ErrorMessage = "Regular price must be zero or greater.")] public decimal Price { get; set; }
  [Range(typeof(decimal), "0.01", "999999999.99", ErrorMessage = "Sale price must be greater than zero.")] public decimal? SalePrice { get; set; }
  [Range(0, int.MaxValue)] [Display(Name = "Stock quantity")] public int StockQuantity { get; set; }
  [Range(0, int.MaxValue)] [Display(Name = "Low-stock threshold")] public int LowStockThreshold { get; set; } = 5;
  [StringLength(80)] public string? Barcode { get; set; }
  [Required(ErrorMessage = "Select a category.")] [Display(Name = "Category")] public int? CategoryId { get; set; }
  [StringLength(120)] public string? Brand { get; set; } = "AARCHIZZ";
  [StringLength(120)] [Display(Name = "Style / model name or code")] public string? StyleCode { get; set; }
  [StringLength(120)] [Display(Name = "Bag type")] public string? ProductType { get; set; }
  [StringLength(120)] [Display(Name = "Pattern / design")] public string? Pattern { get; set; }
  [StringLength(120)] public string? Finish { get; set; }
  [StringLength(1000)] public string? Tags { get; set; }
  [StringLength(120)] public string? Material { get; set; }
  [StringLength(120)] [Display(Name = "Lining material")] public string? LiningMaterial { get; set; }
  [StringLength(80)] public string? Color { get; set; }
  [StringLength(120)] public string? Dimensions { get; set; }
  [Range(typeof(decimal), "0.001", "999999.999")] public decimal? Length { get; set; }
  [Range(typeof(decimal), "0.001", "999999.999")] public decimal? Width { get; set; }
  [Range(typeof(decimal), "0.001", "999999.999")] public decimal? Height { get; set; }
  [StringLength(20)] [Display(Name = "Dimension units")] public string? DimensionUnit { get; set; }
  [Range(typeof(decimal), "0.001", "999999.999")] public decimal? Weight { get; set; }
  [StringLength(20)] [Display(Name = "Weight units")] public string? WeightUnit { get; set; }
  [StringLength(120)] [Display(Name = "Closure type")] public string? ClosureType { get; set; }
  [StringLength(120)] [Display(Name = "Handle / strap type")] public string? HandleStrapType { get; set; }
  [Range(0, 1000)] [Display(Name = "Compartments")] public int? CompartmentCount { get; set; }
  [Range(0, 1000)] [Display(Name = "Pockets")] public int? PocketCount { get; set; }
  [StringLength(500)] public string? Occasion { get; set; }
  [StringLength(2000)] [Display(Name = "Care instructions")] public string? CareInstructions { get; set; }
  [StringLength(2000)] [Display(Name = "Package contents")] public string? PackageContents { get; set; }
  [StringLength(2000)] [Display(Name = "Shipping information")] public string? ShippingInformation { get; set; }
  [StringLength(200)] public string? ShippingPolicyReference { get; set; }
  [StringLength(2000)] [Display(Name = "Return information")] public string? ReturnInformation { get; set; }
  [StringLength(200)] public string? ReturnPolicyReference { get; set; }
  [StringLength(2000)] [Display(Name = "Warranty information")] public string? WarrantyInformation { get; set; }
  [StringLength(180)] [Display(Name = "SEO title")] public string? SeoTitle { get; set; }
  [StringLength(320)] [Display(Name = "SEO meta description")] public string? SeoMetaDescription { get; set; }
  [Display(Name = "Published")] public bool IsPublished { get; set; }
  [Display(Name = "Featured product")] public bool IsFeatured { get; set; }
  public List<IFormFile> Images { get; set; } = [];
  public List<string?> NewImageAltTexts { get; set; } = [];
  public List<int?> NewImageVariantIndexes { get; set; } = [];
  public int? PrimaryNewImageIndex { get; set; }
  public List<AdminProductVariantViewModel> Variants { get; set; } = [];
}

public class AdminProductEditViewModel : AdminProductCreateViewModel
{
  public int Id { get; set; }
  public List<AdminProductImageViewModel> ExistingImages { get; set; } = [];
  public int? PrimaryExistingImageId { get; set; }
}

public class AdminProductVariantViewModel
{
  public int? Id { get; set; }
  [StringLength(80)] public string? Name { get; set; }
  [StringLength(80)] public string? Color { get; set; }
  [StringLength(80)] public string? Size { get; set; }
  [StringLength(80)] public string? Sku { get; set; }
  [Range(typeof(decimal), "0.01", "999999999.99")] public decimal? PriceOverride { get; set; }
  [Range(0, int.MaxValue)] public int StockQuantity { get; set; }
  public bool IsAvailable { get; set; } = true;
}

public class AdminProductImageViewModel
{
  public int Id { get; set; }
  public string UrlOrStorageKey { get; set; } = string.Empty;
  [StringLength(180)] public string? AltText { get; set; }
  public int SortOrder { get; set; }
  public bool IsPrimary { get; set; }
  public bool Remove { get; set; }
  public int? ProductVariantId { get; set; }
}

public class AdminProductDeleteViewModel
{
  public int Id { get; init; }
  public string Name { get; init; } = string.Empty;
  public string Sku { get; init; } = string.Empty;
}

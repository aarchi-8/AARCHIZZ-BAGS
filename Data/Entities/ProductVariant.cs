using System.ComponentModel.DataAnnotations;

namespace AARCHIZZ.Data.Entities;

public class ProductVariant
{
  public int Id { get; set; }

  [Required, MaxLength(80)]
  public string Name { get; set; } = string.Empty;

  [MaxLength(80)]
  public string? Color { get; set; }

  [MaxLength(80)]
  public string? Size { get; set; }

  [MaxLength(80)]
  public string? Sku { get; set; }

  public decimal? PriceOverride { get; set; }

  public int StockQuantity { get; set; }
  public bool IsAvailable { get; set; } = true;

  public int ProductId { get; set; }
  public Product Product { get; set; } = null!;
  public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}

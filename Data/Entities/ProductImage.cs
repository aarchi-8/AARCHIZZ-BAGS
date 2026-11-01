using System.ComponentModel.DataAnnotations;

namespace AARCHIZZ.Data.Entities;

public class ProductImage
{
  public int Id { get; set; }

  [Required, MaxLength(2000)]
  public string UrlOrStorageKey { get; set; } = string.Empty;

  [MaxLength(180)]
  public string? AltText { get; set; }

  public int SortOrder { get; set; }
  public bool IsPrimary { get; set; }

  public int ProductId { get; set; }
  public Product Product { get; set; } = null!;
  public int? ProductVariantId { get; set; }
  public ProductVariant? ProductVariant { get; set; }
}

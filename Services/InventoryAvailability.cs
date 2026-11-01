using AARCHIZZ.Data.Entities;

namespace AARCHIZZ.Services;

public static class InventoryAvailability
{
  public static int GetAvailableQuantity(Product product, ProductVariant? variant = null)
    => GetAvailableQuantity(product.StockQuantity, variant?.StockQuantity ?? 0, variant?.IsAvailable ?? true, variant is not null);

  public static int GetAvailableQuantity(int productStock, int variantStock, bool variantAvailable, bool hasVariant)
  {
    if (hasVariant && !variantAvailable) return 0;

    var normalizedProductStock = Math.Max(0, productStock);
    if (normalizedProductStock > 0)
    {
      var normalizedVariantStock = Math.Max(0, variantStock);
      return hasVariant && normalizedVariantStock > 0
        ? Math.Min(normalizedProductStock, normalizedVariantStock)
        : normalizedProductStock;
    }

    return 0;
  }

  public static bool IsAvailable(Product product) =>
    product.Variants.Count > 0
      ? product.Variants.Any(variant => GetAvailableQuantity(product, variant) > 0)
      : GetAvailableQuantity(product) > 0;
}

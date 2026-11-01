namespace AARCHIZZ.Models;

public sealed class ShoppingCartViewModel
{
  public IReadOnlyList<ShoppingCartItemViewModel> Items { get; init; } = [];
  public decimal Subtotal { get; init; }
  public string CurrencyCode { get; init; } = "INR";
  public int ItemCount { get; init; }
}

public sealed class ShoppingCartItemViewModel
{
  public int ProductId { get; init; }
  public int? VariantId { get; init; }
  public string Name { get; init; } = string.Empty;
  public string Slug { get; init; } = string.Empty;
  public string ImageUrl { get; init; } = string.Empty;
  public string? OrderImageUrl { get; init; }
  public string? VariantLabel { get; init; }
  public decimal UnitPrice { get; init; }
  public decimal LineTotal { get; init; }
  public string CurrencyCode { get; init; } = "INR";
  public int Quantity { get; init; }
  public int AvailableQuantity { get; init; }
  public bool IsAvailable { get; init; }
  public bool CanAdjustQuantity { get; init; }
  public string? AvailabilityMessage { get; init; }
}

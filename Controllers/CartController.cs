using AARCHIZZ.Services;
using Microsoft.AspNetCore.Mvc;

namespace AARCHIZZ.Controllers;

[Route("cart")]
public sealed class CartController : Controller
{
  private readonly ShoppingCartService _cart;
  private readonly WhatsAppOrderService _whatsApp;

  public CartController(ShoppingCartService cart, WhatsAppOrderService whatsApp)
  {
    _cart = cart;
    _whatsApp = whatsApp;
  }

  [HttpGet("")]
  public async Task<IActionResult> Index()
  {
    return View(await _cart.GetAsync(HttpContext.Session, HttpContext.RequestAborted));
  }

  [HttpPost("add")]
  [ValidateAntiForgeryToken]
  public async Task<IActionResult> Add(int productId, int? variantId, int quantity = 1, string? returnUrl = null)
  {
    var error = await _cart.AddAsync(HttpContext.Session, productId, variantId, quantity, HttpContext.RequestAborted);
    if (error is null)
    {
      TempData["CartMessage"] = "Added to your bag.";
      return SafeRedirect(returnUrl);
    }

    TempData["CartError"] = error;
    return RedirectToAction(nameof(Index));
  }

  [HttpPost("buy-now")]
  [ValidateAntiForgeryToken]
  public async Task<IActionResult> BuyNow(int productId, int? variantId, int quantity = 1)
  {
    var (item, validationError) = await _cart.GetOrderItemAsync(productId, variantId, quantity, HttpContext.RequestAborted);
    if (validationError is not null || item is null)
    {
      TempData["CartError"] = validationError ?? "This product is currently unavailable.";
      return RedirectToAction(nameof(Index));
    }

    if (!_whatsApp.TryCreateProductUrl(item.Slug, out var productUrl, out var developmentOnly, out var urlError))
    {
      TempData["CartError"] = urlError;
      return RedirectToAction(nameof(Index));
    }

    if (!_whatsApp.TryCreateImageUrl(item.ImageUrl, out var imageUrl, out var imageError))
    {
      TempData["CartError"] = imageError;
      return RedirectToAction(nameof(Index));
    }

    var message = WhatsAppOrderService.BuildProductMessage(item with { ImageUrl = imageUrl }, productUrl, developmentOnly);
    if (!_whatsApp.TryCreateLink(message, out var whatsAppUrl, out var configurationError))
    {
      TempData["CartError"] = configurationError;
      return RedirectToAction(nameof(Index));
    }

    return Redirect(whatsAppUrl);
  }

  [HttpPost("order-on-whatsapp")]
  [ValidateAntiForgeryToken]
  public async Task<IActionResult> OrderOnWhatsApp()
  {
    var cart = await _cart.GetAsync(HttpContext.Session, HttpContext.RequestAborted);
    if (cart.Items.Count == 0)
    {
      TempData["CartError"] = "Your bag is empty. Add an available item before creating a WhatsApp enquiry.";
      return RedirectToAction(nameof(Index));
    }

    var unavailable = cart.Items.FirstOrDefault(item => !item.IsAvailable);
    if (unavailable is not null)
    {
      TempData["CartError"] = $"Please review your bag before ordering: {unavailable.Name} — {unavailable.AvailabilityMessage ?? "This item is no longer available."}";
      return RedirectToAction(nameof(Index));
    }

    var messageItems = new List<WhatsAppCartMessageItem>(cart.Items.Count);
    var developmentOnly = false;
    foreach (var item in cart.Items)
    {
      if (!_whatsApp.TryCreateProductUrl(item.Slug, out var productUrl, out var itemDevelopmentOnly, out var urlError))
      {
        TempData["CartError"] = urlError;
        return RedirectToAction(nameof(Index));
      }
      developmentOnly |= itemDevelopmentOnly;

      if (!_whatsApp.TryCreateImageUrl(item.OrderImageUrl, out var imageUrl, out var imageError))
      {
        TempData["CartError"] = imageError;
        return RedirectToAction(nameof(Index));
      }

      messageItems.Add(new WhatsAppCartMessageItem(
        new WhatsAppOrderLine(item.Name, item.Slug, item.VariantLabel, item.Quantity, item.UnitPrice, item.CurrencyCode),
        productUrl,
        imageUrl));
    }

    var message = WhatsAppOrderService.BuildCartMessage(messageItems, developmentOnly);
    if (!_whatsApp.TryCreateLink(message, out var whatsAppUrl, out var configurationError))
    {
      TempData["CartError"] = configurationError;
      return RedirectToAction(nameof(Index));
    }

    return Redirect(whatsAppUrl);
  }

  [HttpPost("update")]
  [ValidateAntiForgeryToken]
  public async Task<IActionResult> Update(int productId, int? variantId, int quantity)
  {
    var error = await _cart.UpdateAsync(HttpContext.Session, productId, variantId, quantity, HttpContext.RequestAborted);
    TempData[error is null ? "CartMessage" : "CartError"] = error ?? "Your bag has been updated.";
    return RedirectToAction(nameof(Index));
  }

  [HttpPost("remove")]
  [ValidateAntiForgeryToken]
  public IActionResult Remove(int productId, int? variantId)
  {
    _cart.Remove(HttpContext.Session, productId, variantId);
    TempData["CartMessage"] = "Item removed from your bag.";
    return RedirectToAction(nameof(Index));
  }

  private IActionResult SafeRedirect(string? returnUrl)
  {
    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
    return RedirectToAction(nameof(Index));
  }

}

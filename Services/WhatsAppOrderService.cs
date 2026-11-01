using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AARCHIZZ.Services;

public sealed class WhatsAppOptions
{
  public const string SectionName = "WhatsApp";
  public string? BusinessNumber { get; set; }
}

public sealed class StoreOptions
{
  public const string SectionName = "Store";
  public string? PublicBaseUrl { get; set; }
  public string? LocationUrl { get; set; }
}

public sealed class WhatsAppOrderService(
  IOptions<WhatsAppOptions> options,
  IOptions<StoreOptions> storeOptions,
  IHostEnvironment environment,
  IHttpContextAccessor httpContextAccessor)
{
  private static readonly Regex DigitsOnly = new("^[0-9]{8,15}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

  public bool TryCreateLink(string message, out string link, out string error)
  {
    var number = options.Value.BusinessNumber;
    if (string.IsNullOrWhiteSpace(number) || !DigitsOnly.IsMatch(number))
    {
      link = string.Empty;
      error = "WhatsApp ordering is temporarily unavailable because the store contact number is not configured correctly.";
      return false;
    }

    link = $"https://wa.me/{number}?text={Uri.EscapeDataString(message)}";
    error = string.Empty;
    return true;
  }

  public bool TryCreateContactLink(out string link, out string error)
  {
    var number = options.Value.BusinessNumber;
    if (string.IsNullOrWhiteSpace(number) || !DigitsOnly.IsMatch(number))
    {
      link = string.Empty;
      error = "WhatsApp contact is temporarily unavailable because the store contact number is not configured correctly.";
      return false;
    }

    link = $"https://wa.me/{number}";
    error = string.Empty;
    return true;
  }

  public bool TryCreateProductUrl(string slug, out string url, out bool developmentOnly, out string error)
  {
    developmentOnly = false;
    if (string.IsNullOrWhiteSpace(slug) || slug.Contains('/') || slug.Contains('\\'))
    {
      url = string.Empty;
      error = "We could not create a link to this product. Please try again.";
      return false;
    }

    if (!TryGetBaseUri(out var baseUri, out developmentOnly, out error))
    {
      url = string.Empty;
      return false;
    }

    url = new Uri(baseUri!, $"shop/{Uri.EscapeDataString(slug)}").AbsoluteUri;
    return true;
  }

  public bool TryCreateImageUrl(string? imagePath, out string? url, out string error)
  {
    url = null;
    error = string.Empty;
    if (string.IsNullOrWhiteSpace(imagePath)) return true;

    if (Uri.TryCreate(imagePath, UriKind.Absolute, out var imageUri))
    {
      var allowedScheme = imageUri.Scheme == Uri.UriSchemeHttps ||
        (environment.IsDevelopment() && imageUri.Scheme == Uri.UriSchemeHttp);
      if (allowedScheme && (environment.IsDevelopment() || IsPublicHost(imageUri)))
      {
        url = imageUri.AbsoluteUri;
        return true;
      }

      error = "The product image must use a public HTTPS URL for production WhatsApp orders.";
      return false;
    }

    if (!imagePath.StartsWith('/') || imagePath.StartsWith("//", StringComparison.Ordinal))
    {
      error = "The product image path is not a valid public image link.";
      return false;
    }

    if (!TryGetBaseUri(out var baseUri, out _, out error)) return false;
    url = new Uri(baseUri!, imagePath.TrimStart('/')).AbsoluteUri;
    return true;
  }

  private bool TryGetBaseUri(out Uri? baseUri, out bool developmentOnly, out string error)
  {
    developmentOnly = false;
    var configuredBase = storeOptions.Value.PublicBaseUrl?.Trim();
    if (string.IsNullOrWhiteSpace(configuredBase))
    {
      if (!environment.IsDevelopment())
      {
        baseUri = null;
        error = "WhatsApp ordering needs a valid public HTTPS website address. Configure Store:PublicBaseUrl before generating production order links.";
        return false;
      }

      var request = httpContextAccessor.HttpContext?.Request;
      if (request is null || !request.Host.HasValue || request.Scheme is not ("http" or "https") ||
          !Uri.TryCreate($"{request.Scheme}://{request.Host.Value}/", UriKind.Absolute, out baseUri) || !baseUri.IsLoopback)
      {
        baseUri = null;
        error = "A local development URL could not be determined. Open the site using localhost or a loopback address to create a test link.";
        return false;
      }

      developmentOnly = true;
      error = string.Empty;
      return true;
    }

    if (!Uri.TryCreate(configuredBase, UriKind.Absolute, out baseUri) ||
        !string.IsNullOrEmpty(baseUri.UserInfo) ||
        !string.IsNullOrEmpty(baseUri.Query) ||
        !string.IsNullOrEmpty(baseUri.Fragment) ||
        (baseUri.Scheme != Uri.UriSchemeHttps && !(environment.IsDevelopment() && baseUri.Scheme == Uri.UriSchemeHttp)) ||
        (!environment.IsDevelopment() && !IsPublicHost(baseUri)))
    {
      baseUri = null;
      error = environment.IsDevelopment()
        ? "Store:PublicBaseUrl must be an absolute HTTP or HTTPS website URL without credentials, query, or fragment."
        : "Store:PublicBaseUrl must be a publicly accessible HTTPS website URL without credentials, query, or fragment.";
      return false;
    }

    developmentOnly = environment.IsDevelopment() && !IsPublicHost(baseUri);
    baseUri = new Uri(configuredBase.TrimEnd('/') + "/", UriKind.Absolute);
    error = string.Empty;
    return true;
  }

  private static bool IsPublicHost(Uri uri)
  {
    if (uri.IsLoopback) return false;
    var host = uri.DnsSafeHost;
    if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) return false;

    if (uri.HostNameType == UriHostNameType.Dns) return host.Contains('.', StringComparison.Ordinal);
    if (!IPAddress.TryParse(host, out var address)) return false;
    if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast) return false;

    var bytes = address.GetAddressBytes();
    if (address.AddressFamily == AddressFamily.InterNetwork)
    {
      return !(bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 169 && bytes[1] == 254 ||
               bytes[0] == 172 && bytes[1] is >= 16 and <= 31 || bytes[0] == 192 && bytes[1] == 168 ||
               bytes[0] == 0 || bytes[0] >= 224);
    }

    return (bytes[0] & 0xFE) != 0xFC;
  }

  public static string BuildProductMessage(WhatsAppOrderLine item, string productUrl, bool developmentOnly = false)
  {
    var variantLine = string.IsNullOrWhiteSpace(item.VariantLabel) ? string.Empty : $"\n• *Color/Variant:* {item.VariantLabel}";
    var imageLine = string.IsNullOrWhiteSpace(item.ImageUrl) ? string.Empty : $"\n\n🖼️ *Product Image:*\n{item.ImageUrl}";
    return $"{DevelopmentWarning(developmentOnly)}Hello AARCHIZZ! 😊\n\nI'd like to place an order for this bag. 👜\n\n🛍️ *Order Details*\n\n• *Product:* {item.Name}{variantLine}\n• *Quantity:* {item.Quantity}\n• *Unit Price:* {FormatPrice(item.UnitPrice, item.CurrencyCode)}\n• *Total:* {FormatPrice(item.LineTotal, item.CurrencyCode)}\n\n🔗 *Product Link:*\n{productUrl}{imageLine}\n\nPlease confirm availability and share the shipping charges and expected delivery date. Kindly let me know how to proceed with the order.\n\nThank you! ❤️";
  }

  public static string BuildCartMessage(IReadOnlyList<WhatsAppCartMessageItem> items, bool developmentOnly = false)
  {
    var sections = items.Select((entry, index) =>
    {
      var item = entry.Item;
      var variantLine = string.IsNullOrWhiteSpace(item.VariantLabel) ? string.Empty : $"\n• *Color/Variant:* {item.VariantLabel}";
      var imageLine = string.IsNullOrWhiteSpace(entry.ProductImageUrl) ? string.Empty : $"\n• *Product Image:* {entry.ProductImageUrl}";
      return $"*Item {index + 1}*\n• *Product:* {item.Name}{variantLine}\n• *Quantity:* {item.Quantity}\n• *Unit Price:* {FormatPrice(item.UnitPrice, item.CurrencyCode)}\n• *Line Total:* {FormatPrice(item.LineTotal, item.CurrencyCode)}\n• *Product Link:* {entry.ProductUrl}{imageLine}";
    });

    var subtotals = items.GroupBy(entry => entry.Item.CurrencyCode, StringComparer.OrdinalIgnoreCase)
      .Select(group => $"{group.Key} {FormatPrice(group.Sum(entry => entry.Item.LineTotal), group.Key)}");

    return $"{DevelopmentWarning(developmentOnly)}Hello AARCHIZZ! 😊\n\nI'd like to place an order for the following bags:\n\n🛍️ *MY ORDER*\n\n{string.Join("\n\n", sections)}\n\n💰 *Order Subtotal:* {string.Join("; ", subtotals)}\n\nShipping charges, if applicable, can be confirmed by the store.\n\nPlease confirm the availability of these items and share the shipping charges, delivery details, and estimated delivery date.\n\nThank you for your assistance! ❤️";
  }

  private static string DevelopmentWarning(bool developmentOnly) => developmentOnly
    ? "⚠️ *DEVELOPMENT TEST ONLY: Local links below are not publicly accessible.*\n\n"
    : string.Empty;

  public static string FormatPrice(decimal amount, string currencyCode)
  {
    var formatted = amount.ToString("N2", CultureInfo.InvariantCulture);
    return currencyCode.Equals("INR", StringComparison.OrdinalIgnoreCase)
      ? $"\u20B9{formatted}"
      : $"{formatted} {currencyCode.ToUpperInvariant()}";
  }
}

public sealed record WhatsAppOrderLine(
  string Name,
  string Slug,
  string? VariantLabel,
  int Quantity,
  decimal UnitPrice,
  string CurrencyCode,
  string? ImageUrl = null)
{
  public decimal LineTotal => UnitPrice * Quantity;
}

public sealed record WhatsAppCartMessageItem(WhatsAppOrderLine Item, string ProductUrl, string? ProductImageUrl = null);

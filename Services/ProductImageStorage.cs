using Microsoft.AspNetCore.Http;

namespace AARCHIZZ.Services;

public interface IProductImageStorage
{
  Task<StoredProductImage> SaveAsync(IFormFile file, CancellationToken cancellationToken = default);
  Task<StoredProductImage> SaveCategoryImageAsync(IFormFile file, CancellationToken cancellationToken = default);
  Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}

public sealed record StoredProductImage(string StorageKey, string ContentType);

public sealed class LocalProductImageStorage : IProductImageStorage
{
  private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
  private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };
  private const long MaxProductFileSize = 10 * 1024 * 1024;
  private const long MaxCategoryFileSize = 5 * 1024 * 1024;
  private readonly string _root;

  public LocalProductImageStorage(IConfiguration configuration)
  {
    _root = configuration["AARCHIZZ_UPLOAD_ROOT"]
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AARCHIZZ", "uploads");
    Directory.CreateDirectory(_root);
  }

  public Task<StoredProductImage> SaveAsync(IFormFile file, CancellationToken cancellationToken = default) =>
    SaveAsync(file, "products", MaxProductFileSize, cancellationToken);

  public Task<StoredProductImage> SaveCategoryImageAsync(IFormFile file, CancellationToken cancellationToken = default) =>
    SaveAsync(file, "categories", MaxCategoryFileSize, cancellationToken);

  private async Task<StoredProductImage> SaveAsync(IFormFile file, string collection, long maxFileSize, CancellationToken cancellationToken)
  {
    if (file.Length <= 0 || file.Length > maxFileSize)
    {
      var sizeInMb = maxFileSize / (1024 * 1024);
      throw new InvalidDataException($"The image must be between 1 byte and {sizeInMb} MB.");
    }

    var extension = Path.GetExtension(file.FileName);
    var expectedContentType = extension.ToLowerInvariant() switch
    {
      ".jpg" or ".jpeg" => "image/jpeg",
      ".png" => "image/png",
      ".webp" => "image/webp",
      _ => null
    };
    if (!AllowedExtensions.Contains(extension) || expectedContentType is null ||
        !AllowedContentTypes.Contains(file.ContentType) || !expectedContentType.Equals(file.ContentType, StringComparison.OrdinalIgnoreCase))
    {
      throw new InvalidDataException("Choose a JPG, JPEG, PNG, or WebP image with matching image content.");
    }

    await using var input = file.OpenReadStream();
    var header = new byte[12];
    var read = await input.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
    if (!HasValidSignature(header, read, expectedContentType))
    {
      throw new InvalidDataException("The uploaded file is not a valid image.");
    }

    var relativeDirectory = Path.Combine(collection, DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
    var absoluteDirectory = Path.Combine(_root, relativeDirectory);
    Directory.CreateDirectory(absoluteDirectory);
    var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
    var absolutePath = Path.Combine(absoluteDirectory, fileName);
    await using (var output = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
    {
      input.Position = 0;
      await input.CopyToAsync(output, cancellationToken);
    }

    return new StoredProductImage($"/uploads/{relativeDirectory.Replace(Path.DirectorySeparatorChar, '/')}/{fileName}", file.ContentType);
  }

  public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
  {
    if (!storageKey.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
    {
      return Task.CompletedTask;
    }

    var relativePath = storageKey["/uploads/".Length..].Replace('/', Path.DirectorySeparatorChar);
    var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));
    var rootPath = Path.GetFullPath(_root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
    {
      File.Delete(fullPath);
    }

    return Task.CompletedTask;
  }

  private static bool HasValidSignature(byte[] header, int length, string contentType) => contentType switch
  {
    "image/jpeg" => length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
    "image/png" => length >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
    "image/webp" => length >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
    _ => false
  };
}

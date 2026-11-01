using System.Security.Cryptography;
using System.Text;
using AARCHIZZ.Data;
using AARCHIZZ.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AARCHIZZ.Services;

public class AdminCredentialValidator
{
  private const int SaltSize = 16;
  private const int HashSize = 32;
  private readonly string? _username;
  private readonly string? _passwordHash;
  private readonly AARCHIZZDbContext _db;

  public AdminCredentialValidator(IConfiguration configuration, AARCHIZZDbContext db)
  {
    _username = configuration["AARCHIZZ_ADMIN_USERNAME"];
    _passwordHash = configuration["AARCHIZZ_ADMIN_PASSWORD_HASH"];
    _db = db;
  }

  public bool IsConfigured => !string.IsNullOrWhiteSpace(_username) && !string.IsNullOrWhiteSpace(_passwordHash);

  public bool Validate(string username, string password)
  {
    if (!IsConfigured || string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
    {
      return false;
    }

    var usernameMatches = CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(username.Trim().ToUpperInvariant()),
        Encoding.UTF8.GetBytes(_username!.Trim().ToUpperInvariant()));

    return usernameMatches && VerifyPassword(password, _passwordHash!);
  }

  public async Task<bool> ValidateDatabaseAsync(string username, string password, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
    {
      return false;
    }

    var owner = await _db.OwnerAccounts.SingleOrDefaultAsync(
        account => account.SingletonMarker == OwnerAccount.SingletonMarkerValue,
        cancellationToken);
    if (owner is null)
    {
      return false;
    }

    var usernameMatches = CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(username.Trim().ToUpperInvariant()),
        Encoding.UTF8.GetBytes(owner.NormalizedUsername));
    if (!usernameMatches || !VerifyPassword(password, owner.PasswordHash))
    {
      return false;
    }

    owner.LastLoginUtc = DateTime.UtcNow;
    owner.UpdatedUtc = owner.LastLoginUtc.Value;
    await _db.SaveChangesAsync(cancellationToken);
    return true;
  }

  public async Task<OwnerAccount?> GetOwnerAsync(CancellationToken cancellationToken = default)
  {
    return await _db.OwnerAccounts.SingleOrDefaultAsync(
        account => account.SingletonMarker == OwnerAccount.SingletonMarkerValue,
        cancellationToken);
  }

  public async Task<bool> VerifyCurrentPasswordAsync(string password, CancellationToken cancellationToken = default)
  {
    var owner = await GetOwnerAsync(cancellationToken);
    return owner is not null && !string.IsNullOrEmpty(password) && VerifyPassword(password, owner.PasswordHash);
  }

  public async Task<(bool Succeeded, string? Error)> UpdateOwnerAsync(
      string currentPassword,
      string newUsername,
      string? newPassword,
      CancellationToken cancellationToken = default)
  {
    var owner = await GetOwnerAsync(cancellationToken);
    if (owner is null || !VerifyPassword(currentPassword, owner.PasswordHash))
    {
      return (false, "The current password is not valid.");
    }

    var normalizedUsername = newUsername.Trim().ToUpperInvariant();
    if (string.IsNullOrWhiteSpace(normalizedUsername) || normalizedUsername.Length > 120)
    {
      return (false, "Enter a valid username.");
    }

    owner.Username = newUsername.Trim();
    owner.NormalizedUsername = normalizedUsername;
    if (!string.IsNullOrEmpty(newPassword))
    {
      owner.PasswordHash = CreatePasswordHash(newPassword);
    }

    owner.SecurityStamp = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    owner.UpdatedUtc = DateTime.UtcNow;

    try
    {
      await _db.SaveChangesAsync(cancellationToken);
      return (true, null);
    }
    catch (DbUpdateException)
    {
      return (false, "That username is already in use.");
    }
  }

  private static bool VerifyPassword(string password, string encodedHash)
  {
    var parts = encodedHash.Split('$', StringSplitOptions.None);
    if (parts.Length != 4 || !parts[0].Equals("PBKDF2-SHA256", StringComparison.Ordinal))
    {
      return false;
    }

    if (!int.TryParse(parts[1], out var iterations) || iterations < 100_000 || iterations > 2_000_000)
    {
      return false;
    }

    try
    {
      var salt = Convert.FromBase64String(parts[2]);
      var expectedHash = Convert.FromBase64String(parts[3]);
      if (salt.Length != SaltSize || expectedHash.Length != HashSize)
      {
        return false;
      }

      var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSize);
      return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
    catch (FormatException)
    {
      return false;
    }
  }

  private static string CreatePasswordHash(string password)
  {
    var salt = RandomNumberGenerator.GetBytes(SaltSize);
    var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600_000, HashAlgorithmName.SHA256, HashSize);
    return $"PBKDF2-SHA256$600000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
  }
}
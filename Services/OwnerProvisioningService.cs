using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using AARCHIZZ.Data;
using AARCHIZZ.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AARCHIZZ.Services;

public class OwnerProvisioningService
{
  private const int PasswordIterations = 600_000;
  private const int SaltSize = 16;
  private const int HashSize = 32;
  private readonly AARCHIZZDbContext _db;

  public OwnerProvisioningService(AARCHIZZDbContext db)
  {
    _db = db;
  }

  public async Task<int> RunAsync()
  {
    Console.WriteLine("AARCHIZZ owner provisioning");
    Console.Write("Owner username: ");
    var username = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(username) || username.Length > 120)
    {
      Console.Error.WriteLine("A valid owner username is required.");
      return 1;
    }

    Console.Write("Owner password: ");
    var password = ReadHiddenInput();
    Console.WriteLine();
    Console.Write("Confirm password: ");
    var confirmation = ReadHiddenInput();
    Console.WriteLine();

    if (password.Length < 12 || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(confirmation)))
    {
      Console.Error.WriteLine("The password must be at least 12 characters and both entries must match.");
      return 1;
    }

    try
    {
      if (await _db.OwnerAccounts.AnyAsync())
      {
        Console.Error.WriteLine("An owner account already exists. No credentials were changed.");
        return 1;
      }

      var now = DateTime.UtcNow;
      _db.OwnerAccounts.Add(new OwnerAccount
      {
        Username = username,
        NormalizedUsername = username.ToUpperInvariant(),
        PasswordHash = CreatePasswordHash(password),
        SecurityStamp = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        SingletonMarker = OwnerAccount.SingletonMarkerValue,
        CreatedUtc = now,
        UpdatedUtc = now
      });

      await _db.SaveChangesAsync();
      Console.WriteLine("Owner account created successfully.");
      return 0;
    }
    catch (DbException)
    {
      Console.Error.WriteLine("Owner provisioning could not access the configured database. Apply the owner-account migration first, then retry.");
      return 1;
    }
    finally
    {
      CryptographicOperations.ZeroMemory(Encoding.UTF8.GetBytes(password));
      CryptographicOperations.ZeroMemory(Encoding.UTF8.GetBytes(confirmation));
    }
  }

  private static string CreatePasswordHash(string password)
  {
    var salt = RandomNumberGenerator.GetBytes(SaltSize);
    var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, HashSize);
    return $"PBKDF2-SHA256${PasswordIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
  }

  private static string ReadHiddenInput()
  {
    var value = new StringBuilder();
    while (true)
    {
      var key = Console.ReadKey(intercept: true);
      if (key.Key == ConsoleKey.Enter)
      {
        break;
      }

      if (key.Key == ConsoleKey.Backspace)
      {
        if (value.Length > 0)
        {
          value.Length--;
        }

        continue;
      }

      if (!char.IsControl(key.KeyChar))
      {
        value.Append(key.KeyChar);
      }
    }

    return value.ToString();
  }
}
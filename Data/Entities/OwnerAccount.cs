using System.ComponentModel.DataAnnotations;

namespace AARCHIZZ.Data.Entities;

public class OwnerAccount
{
  public const string SingletonMarkerValue = "OWNER";

  public int Id { get; set; }

  [Required, MaxLength(120)]
  public string Username { get; set; } = string.Empty;

  [Required, MaxLength(120)]
  public string NormalizedUsername { get; set; } = string.Empty;

  [Required, MaxLength(200)]
  public string PasswordHash { get; set; } = string.Empty;

  [Required, MaxLength(100)]
  public string SecurityStamp { get; set; } = string.Empty;

  [Required, MaxLength(16)]
  public string SingletonMarker { get; set; } = SingletonMarkerValue;

  public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
  public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
  public DateTime? LastLoginUtc { get; set; }
}
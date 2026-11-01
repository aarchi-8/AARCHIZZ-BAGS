using System.ComponentModel.DataAnnotations;

namespace AARCHIZZ.Models;

public class AdminAccountViewModel
{
  public string CurrentUsername { get; set; } = string.Empty;

  [Required]
  [Display(Name = "Current password")]
  [DataType(DataType.Password)]
  public string CurrentPassword { get; set; } = string.Empty;

  [Required]
  [StringLength(120, MinimumLength = 3)]
  [Display(Name = "New username")]
  public string NewUsername { get; set; } = string.Empty;

  [DataType(DataType.Password)]
  [Display(Name = "New password")]
  public string? NewPassword { get; set; }

  [DataType(DataType.Password)]
  [Display(Name = "Confirm new password")]
  [Compare(nameof(NewPassword), ErrorMessage = "The new passwords do not match.")]
  public string? ConfirmNewPassword { get; set; }
}
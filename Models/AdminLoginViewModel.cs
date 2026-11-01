using System.ComponentModel.DataAnnotations;

namespace AARCHIZZ.Models;

public class AdminLoginViewModel
{
  [Required]
  [Display(Name = "Username")]
  public string Username { get; set; } = string.Empty;

  [Required]
  [DataType(DataType.Password)]
  [Display(Name = "Password")]
  public string Password { get; set; } = string.Empty;

  public string? ReturnUrl { get; set; }
}
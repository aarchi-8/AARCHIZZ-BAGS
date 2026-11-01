using System.ComponentModel.DataAnnotations;

namespace AARCHIZZ.Data.Entities;

public class Category
{
  public int Id { get; set; }

  [Required, MaxLength(120)]
  public string Name { get; set; } = string.Empty;

  [Required, MaxLength(120)]
  public string Slug { get; set; } = string.Empty;

  [MaxLength(500)]
  public string? Description { get; set; }

  [MaxLength(1000)]
  public string? ImageUrl { get; set; }

  public bool IsPublished { get; set; } = true;

  public ICollection<Product> Products { get; set; } = new List<Product>();
}
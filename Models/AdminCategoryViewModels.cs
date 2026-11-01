using System.ComponentModel.DataAnnotations;
using AARCHIZZ.Data.Entities;
using Microsoft.AspNetCore.Http;

namespace AARCHIZZ.Models;

public class AdminCategoryListViewModel
{
  public IReadOnlyList<Category> Categories { get; init; } = [];
}

public class AdminCategoryFormViewModel
{
  public int Id { get; set; }

  [Required, StringLength(120)]
  public string Name { get; set; } = string.Empty;

  [Required, StringLength(120)]
  public string Slug { get; set; } = string.Empty;

  [StringLength(500)]
  public string? Description { get; set; }

  [Display(Name = "Upload Category Image")]
  public IFormFile? ImageFile { get; set; }

  public string? CurrentImageUrl { get; set; }

  [Display(Name = "Published")]
  public bool IsPublished { get; set; }
}

public class AdminCategoryDeleteViewModel
{
  public int Id { get; init; }
  public string Name { get; init; } = string.Empty;
  public int ProductCount { get; init; }
}

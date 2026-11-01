using AARCHIZZ.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AARCHIZZ.Data;

public class AARCHIZZDbContext : DbContext
{
  public AARCHIZZDbContext(DbContextOptions<AARCHIZZDbContext> options) : base(options)
  {
  }

  public DbSet<Product> Products => Set<Product>();
  public DbSet<Category> Categories => Set<Category>();
  public DbSet<ProductImage> ProductImages => Set<ProductImage>();
  public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
  public DbSet<OwnerAccount> OwnerAccounts => Set<OwnerAccount>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<Category>(entity =>
    {
      entity.HasIndex(category => category.Slug).IsUnique();
      entity.Property(category => category.Name).IsRequired();
    });

    modelBuilder.Entity<Product>(entity =>
    {
      entity.HasIndex(product => product.Slug).IsUnique();
      entity.HasIndex(product => product.Sku).IsUnique();
      entity.Property(product => product.Price).HasPrecision(18, 2);
      entity.Property(product => product.SalePrice).HasPrecision(18, 2);
      entity.Property(product => product.CurrencyCode).HasDefaultValue("INR");
      entity.Property(product => product.StyleCode).HasMaxLength(120);
      entity.Property(product => product.Pattern).HasMaxLength(120);
      entity.Property(product => product.Finish).HasMaxLength(120);
      entity.Property(product => product.LowStockThreshold).HasDefaultValue(5);
      entity.Property(product => product.Length).HasPrecision(10, 3);
      entity.Property(product => product.Width).HasPrecision(10, 3);
      entity.Property(product => product.Height).HasPrecision(10, 3);
      entity.Property(product => product.Weight).HasPrecision(10, 3);
      entity.HasOne(product => product.Category)
              .WithMany(category => category.Products)
              .HasForeignKey(product => product.CategoryId)
              .OnDelete(DeleteBehavior.Restrict);
    });

    modelBuilder.Entity<ProductImage>(entity =>
    {
      entity.HasIndex(image => new { image.ProductId, image.SortOrder });
      entity.HasOne(image => image.Product)
              .WithMany(product => product.Images)
              .HasForeignKey(image => image.ProductId)
              .OnDelete(DeleteBehavior.Cascade);
      entity.HasOne(image => image.ProductVariant)
              .WithMany(variant => variant.Images)
              .HasForeignKey(image => image.ProductVariantId)
              .OnDelete(DeleteBehavior.NoAction);
    });

    modelBuilder.Entity<ProductVariant>(entity =>
    {
      entity.HasIndex(variant => new { variant.ProductId, variant.Sku }).IsUnique();
      entity.Property(variant => variant.PriceOverride).HasPrecision(18, 2);
      entity.HasOne(variant => variant.Product)
              .WithMany(product => product.Variants)
              .HasForeignKey(variant => variant.ProductId)
              .OnDelete(DeleteBehavior.Cascade);
    });

    modelBuilder.Entity<OwnerAccount>(entity =>
    {
      entity.HasIndex(account => account.NormalizedUsername).IsUnique();
      entity.HasIndex(account => account.SingletonMarker).IsUnique();
      entity.ToTable(table => table.HasCheckConstraint("CK_OwnerAccounts_SingletonMarker", $"[SingletonMarker] = '{OwnerAccount.SingletonMarkerValue}'"));
    });
  }
}

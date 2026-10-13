using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MarketItem> MarketItems => Set<MarketItem>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Production> Productions => Set<Production>();
    public DbSet<Sale> Sales => Set<Sale>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MarketItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(64);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Unit).HasMaxLength(32).IsRequired();
            entity.Property(item => item.PackageQty).HasPrecision(18, 4);
            entity.Property(item => item.PackagePrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(recipe => recipe.Id);
            entity.Property(recipe => recipe.Id).HasMaxLength(64);
            entity.Property(recipe => recipe.Name).HasMaxLength(200).IsRequired();
            entity.Property(recipe => recipe.Category).HasMaxLength(100).IsRequired();
            entity.Property(recipe => recipe.Yield).HasPrecision(18, 4);
            entity.Property(recipe => recipe.HoursPerBatch).HasPrecision(18, 4);
        });

        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasKey(ingredient => ingredient.Id);
            entity.Property(ingredient => ingredient.Id).HasMaxLength(64);
            entity.Property(ingredient => ingredient.RecipeId).HasMaxLength(64);
            entity.Property(ingredient => ingredient.MarketItemId).HasMaxLength(64);
            entity.Property(ingredient => ingredient.Quantity).HasPrecision(18, 4);
            entity.HasOne<Recipe>().WithMany(recipe => recipe.Ingredients)
                .HasForeignKey(ingredient => ingredient.RecipeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<MarketItem>().WithMany(item => item.Ingredients)
                .HasForeignKey(ingredient => ingredient.MarketItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(ingredient => new { ingredient.RecipeId, ingredient.MarketItemId }).IsUnique();
        });

        modelBuilder.Entity<Production>(entity =>
        {
            entity.HasKey(production => production.Id);
            entity.Property(production => production.Id).HasMaxLength(64);
            entity.Property(production => production.RecipeId).HasMaxLength(64);
            entity.Property(production => production.RecipeName).HasMaxLength(200).IsRequired();
            entity.Property(production => production.Date).HasColumnType("date");
            entity.Property(production => production.Quantity).HasPrecision(18, 4);
            entity.Property(production => production.ActualYield).HasPrecision(18, 4);
            entity.Property(production => production.Losses).HasPrecision(18, 4);
            entity.Property(production => production.Margin).HasPrecision(18, 4);
            entity.Property(production => production.PackageQty).HasPrecision(18, 4);
            entity.Property(production => production.YieldPerBatch).HasPrecision(18, 4);
            entity.Property(production => production.IngredientCostPerBatch).HasPrecision(18, 2);
            entity.Property(production => production.ExpectedSalePrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasKey(sale => sale.Id);
            entity.Property(sale => sale.Id).HasMaxLength(64);
            entity.Property(sale => sale.ProductionId).HasMaxLength(64);
            entity.Property(sale => sale.RecipeName).HasMaxLength(200).IsRequired();
            entity.Property(sale => sale.Customer).HasMaxLength(200).IsRequired();
            entity.Property(sale => sale.Notes).HasMaxLength(2000).IsRequired();
            entity.Property(sale => sale.ProductionDate).HasColumnType("date");
            entity.Property(sale => sale.Date).HasColumnType("date");
            entity.Property(sale => sale.PricePerPackage).HasPrecision(18, 2);
            entity.Property(sale => sale.CostPerPackage).HasPrecision(18, 2);
            entity.HasOne<Production>().WithMany().HasForeignKey(sale => sale.ProductionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(sale => sale.ProductionId);
        });
    }
}
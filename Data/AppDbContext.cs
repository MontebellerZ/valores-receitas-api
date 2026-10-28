using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AccountUser>(options)
{
    public DbSet<MarketItem> MarketItems => Set<MarketItem>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Production> Productions => Set<Production>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<StockLot> StockLots => Set<StockLot>();
    public DbSet<StockConsumption> StockConsumptions => Set<StockConsumption>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MarketItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(64);
            entity.Property(item => item.UserId).HasMaxLength(450);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Unit).HasMaxLength(32).IsRequired();
            entity.Property(item => item.PackageQty).HasPrecision(18, 4);
            entity.Property(item => item.PackagePrice).HasPrecision(18, 2);
            entity.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.UserId);
        });

        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(recipe => recipe.Id);
            entity.Property(recipe => recipe.Id).HasMaxLength(64);
            entity.Property(recipe => recipe.UserId).HasMaxLength(450);
            entity.Property(recipe => recipe.Name).HasMaxLength(200).IsRequired();
            entity.Property(recipe => recipe.Category).HasMaxLength(100).IsRequired();
            entity.Property(recipe => recipe.Yield).HasPrecision(18, 4);
            entity.Property(recipe => recipe.HoursPerBatch).HasPrecision(18, 4);
            entity.HasOne<AccountUser>().WithMany().HasForeignKey(recipe => recipe.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(recipe => recipe.UserId);
        });

        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasKey(ingredient => ingredient.Id);
            entity.Property(ingredient => ingredient.Id).HasMaxLength(64);
            entity.Property(ingredient => ingredient.UserId).HasMaxLength(450);
            entity.Property(ingredient => ingredient.RecipeId).HasMaxLength(64);
            entity.Property(ingredient => ingredient.MarketItemId).HasMaxLength(64);
            entity.Property(ingredient => ingredient.Quantity).HasPrecision(18, 4);
            entity.HasOne<AccountUser>().WithMany().HasForeignKey(ingredient => ingredient.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Recipe>().WithMany(recipe => recipe.Ingredients)
                .HasForeignKey(ingredient => ingredient.RecipeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<MarketItem>().WithMany(item => item.Ingredients)
                .HasForeignKey(ingredient => ingredient.MarketItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(ingredient => new { ingredient.RecipeId, ingredient.MarketItemId }).IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(ingredient => ingredient.UserId);
        });

        modelBuilder.Entity<Production>(entity =>
        {
            entity.HasKey(production => production.Id);
            entity.Property(production => production.Id).HasMaxLength(64);
            entity.Property(production => production.UserId).HasMaxLength(450);
            entity.Property(production => production.RecipeId).HasMaxLength(64);
            entity.Property(production => production.RecipeName).HasMaxLength(200).IsRequired();
            entity.Property(production => production.Date).HasColumnType("date");
            entity.Property(production => production.Quantity).HasPrecision(18, 4);
            entity.Property(production => production.ActualYield).HasPrecision(18, 4);
            entity.Property(production => production.Losses).HasPrecision(18, 4);
            entity.Property(production => production.Margin).HasPrecision(18, 4);
            entity.Property(production => production.PackageQty).HasPrecision(18, 4);
            entity.Property(production => production.YieldPerBatch).HasPrecision(18, 4);
            entity.Property(production => production.IngredientCostPerBatch).HasPrecision(18, 8);
            entity.Property(production => production.TotalCost).HasPrecision(18, 8);
            entity.Property(production => production.ExpectedSalePrice).HasPrecision(18, 2);
            entity.HasOne<AccountUser>().WithMany().HasForeignKey(production => production.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(production => production.UserId);
        });

        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasKey(sale => sale.Id);
            entity.Property(sale => sale.Id).HasMaxLength(64);
            entity.Property(sale => sale.UserId).HasMaxLength(450);
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
            entity.HasOne<AccountUser>().WithMany().HasForeignKey(sale => sale.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(sale => sale.ProductionId);
            entity.HasIndex(sale => sale.UserId);
        });

        modelBuilder.Entity<StockLot>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(64);
            entity.Property(item => item.UserId).HasMaxLength(450);
            entity.Property(item => item.MarketItemId).HasMaxLength(64);
            entity.Property(item => item.ItemName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Unit).HasMaxLength(32).IsRequired();
            entity.Property(item => item.EntryDate).HasColumnType("date");
            entity.Property(item => item.PackageQty).HasPrecision(18, 4);
            entity.Property(item => item.PackagePrice).HasPrecision(18, 2);
            entity.Property(item => item.RemainingQty).HasPrecision(18, 4);
            entity.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MarketItem>().WithMany().HasForeignKey(item => item.MarketItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(item => !item.IsDeleted);
            entity.HasIndex(item => new { item.UserId, item.MarketItemId, item.EntryDate, item.EntryOrder });
        });
        modelBuilder.Entity<StockConsumption>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(64);
            entity.Property(item => item.ProductionId).HasMaxLength(64);
            entity.Property(item => item.StockLotId).HasMaxLength(64);
            entity.Property(item => item.Quantity).HasPrecision(18, 4);
            entity.Property(item => item.Cost).HasPrecision(18, 8);
            entity.Property(item => item.EntryDate).HasColumnType("date");
            entity.Property(item => item.PackageQty).HasPrecision(18, 4);
            entity.Property(item => item.PackagePrice).HasPrecision(18, 2);
            entity.HasOne<Production>().WithMany().HasForeignKey(item => item.ProductionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StockLot>().WithMany().HasForeignKey(item => item.StockLotId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<StockAdjustment>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(64);
            entity.Property(item => item.StockLotId).HasMaxLength(64);
            entity.Property(item => item.PreviousQty).HasPrecision(18, 4);
            entity.Property(item => item.RemainingQty).HasPrecision(18, 4);
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.HasOne<StockLot>().WithMany().HasForeignKey(item => item.StockLotId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<MarketItem>().HasQueryFilter(item => !item.IsDeleted);
        modelBuilder.Entity<Recipe>().HasQueryFilter(item => !item.IsDeleted);
        modelBuilder.Entity<Ingredient>().HasQueryFilter(item => !item.IsDeleted);
        modelBuilder.Entity<Production>().HasQueryFilter(item => !item.IsDeleted);
        modelBuilder.Entity<Sale>().HasQueryFilter(item => !item.IsDeleted);
        modelBuilder.Entity<StockConsumption>().HasQueryFilter(item => !item.IsDeleted);
        modelBuilder.Entity<StockAdjustment>().HasQueryFilter(item => !item.IsDeleted);
        modelBuilder.Entity<AccountUser>().HasQueryFilter(item => !item.IsDeleted);
    }
}
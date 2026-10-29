using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Data;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;
using ValoresReceitas.Api.Repositories;
using Xunit;

namespace ValoresReceitas.Api.Tests;

public sealed class SqlServerQueryTests
{
    [SqlServerIntegrationFact]
    public async Task QueriesAndAggregatesExecuteAgainstAnIsolatedSqlServerDatabase()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("VALORES_TEST_SQLSERVER");
        Assert.False(string.IsNullOrWhiteSpace(configuredConnection));
        var connection = new SqlConnectionStringBuilder(configuredConnection)
        {
            InitialCatalog = $"ValoresReceitasQueryTest_{Guid.NewGuid():N}",
        }.ConnectionString;
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            const string userId = "sqlserver-query-test-user";
            var market = new MarketItem { Id = "item", UserId = userId, Name = "Farinha", Unit = "g" };
            var recipe = new Recipe { Id = "recipe", UserId = userId, Name = "Bolo", Category = "Doce", Yield = 10 };
            var production = new Production
            {
                Id = "production", UserId = userId, Date = new DateOnly(2026, 1, 1),
                RecipeId = recipe.Id, RecipeName = recipe.Name, Quantity = 1, ActualYield = 10,
                PackageQty = 2, YieldPerBatch = 10, IngredientCostPerBatch = 5, TotalCost = 5,
            };
            var lot = new StockLot
            {
                Id = "lot", UserId = userId, MarketItemId = market.Id, ItemName = market.Name,
                Unit = market.Unit, EntryDate = new DateOnly(2025, 12, 1), EntryOrder = 1,
                PackageQty = 100, PackagePrice = 10, RemainingQty = 50, IsActive = true,
            };
            db.Users.Add(new AccountUser
            {
                Id = userId, UserName = "sqlserver-query-test", NormalizedUserName = "SQLSERVER-QUERY-TEST",
                Email = "sqlserver-query-test@example.test", NormalizedEmail = "SQLSERVER-QUERY-TEST@EXAMPLE.TEST",
                EmailConfirmed = true, SecurityStamp = Guid.NewGuid().ToString(),
            });
            db.MarketItems.Add(market);
            db.Recipes.Add(recipe);
            db.Ingredients.Add(new Ingredient { Id = "ingredient", UserId = userId, RecipeId = recipe.Id, MarketItemId = market.Id, Quantity = 50 });
            db.Productions.Add(production);
            db.Sales.Add(new Sale
            {
                Id = "sale", UserId = userId, ProductionId = production.Id, ProductionDate = production.Date,
                RecipeName = production.RecipeName, Date = new DateOnly(2026, 1, 2), Packages = 1,
                PricePerPackage = 2, CostPerPackage = 1, Customer = "Cliente", Paid = true, Notes = "",
            });
            db.StockLots.Add(lot);
            db.StockConsumptions.Add(new StockConsumption
            {
                Id = "consumption", ProductionId = production.Id, StockLotId = lot.Id,
                Quantity = 50, Cost = 5, EntryDate = lot.EntryDate, PackageQty = lot.PackageQty, PackagePrice = lot.PackagePrice,
            });
            await db.SaveChangesAsync();

            var repository = new QueryRepository(db);
            var query = new PageQueryDto { Page = 1, PageSize = 25 };
            Assert.Single((await repository.MarketAsync(userId, query, default)).Items);
            var recipes = await repository.RecipesAsync(userId, query, default);
            Assert.Single(recipes.Items);
            Assert.Single((await repository.ProductionsAsync(userId, query, default)).Items);
            Assert.Single((await repository.SalesAsync(userId, query, default)).Items);
            var salesReport = await repository.SalesReportAsync(userId, query, default);
            Assert.Single(salesReport.Daily.Items);
            Assert.Single(salesReport.TopProducts);
            Assert.Single(salesReport.TopBuyers);
            Assert.Single((await repository.StockAsync(userId, query, default)).Items);
            Assert.Single((await repository.StockTotalsAsync(userId, query, default)).Items);
            Assert.Single((await repository.BalancesAsync(userId, query, default)).Items);
            foreach (var sort in new[] { "packages", "cost", "profit" })
                Assert.Single((await repository.ProductionsAsync(userId, query with { SortBy = sort }, default)).Items);
            foreach (var sort in new[] { "packages", "sold", "periodSold", "price", "periodPrice", "periodRevenue", "periodResult", "available", "revenue", "profit", "cost" })
                Assert.Single((await repository.BalancesAsync(userId, query with { SortBy = sort }, default)).Items);
            Assert.Single((await repository.SalesAsync(userId, query with { SortBy = "total" }, default)).Items);
            Assert.Equal(1, (await repository.SummaryAsync(userId, new(), default)).Counts.Market);
            Assert.NotNull(await repository.ReportAsync(userId, production.Id, query, default));
            Assert.Single((await repository.ConsumptionsAsync(userId, production.Id, query, default)).Items);
            foreach (var sort in new[] { "unit", "quantity", "cost", "packageQty", "packagePrice" })
                Assert.Single((await repository.ConsumptionsAsync(userId, production.Id, query with { SortBy = sort }, default)).Items);
            Assert.Single((await repository.SalesAsync(userId, query with { ProductionId = production.Id }, default)).Items);
            foreach (var sort in new[] { "name", "unit", "quantity" })
                Assert.Single((await repository.IngredientsAsync(userId, recipe.Id, query with { SortBy = sort }, default)).Items);
            Assert.Empty(db.ChangeTracker.Entries());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}

public sealed class SqlServerIntegrationFactAttribute : FactAttribute
{
    public SqlServerIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VALORES_TEST_SQLSERVER")))
            Skip = "Set VALORES_TEST_SQLSERVER to an isolated SQL Server instance for provider integration tests.";
    }
}

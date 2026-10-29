using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Data;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Repositories;
using Xunit;

namespace ValoresReceitas.Api.Tests;

public sealed class SqlServerQueryTests
{
    [SqlServerReadOnlyFact]
    public async Task QueriesAndAggregatesExecuteOnTheConfiguredSqlServerWithoutWrites()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ValoresReceitas.Api.csproj")))
            root = root.Parent;
        Assert.NotNull(root);
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName, "appsettings.json")));
        var connection = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
        var userId = await db.Users.AsNoTracking().Select(user => user.Id).FirstOrDefaultAsync();
        Assert.NotNull(userId);
        var repository = new QueryRepository(db);
        var query = new PageQueryDto { Page = 2, PageSize = 25 };
        Assert.True((await repository.MarketAsync(userId, query, default)).Items.Count <= 25);
        Assert.True((await repository.RecipesAsync(userId, query, default)).Items.Count <= 25);
        Assert.True((await repository.ProductionsAsync(userId, query, default)).Items.Count <= 25);
        Assert.True((await repository.SalesAsync(userId, query, default)).Items.Count <= 25);
        Assert.True((await repository.StockAsync(userId, query, default)).Items.Count <= 25);
        Assert.True((await repository.StockTotalsAsync(userId, query, default)).Items.Count <= 25);
        Assert.True((await repository.BalancesAsync(userId, query, default)).Items.Count <= 25);
        foreach (var sort in new[] { "packages", "cost", "profit" })
            Assert.True((await repository.ProductionsAsync(userId, query with { SortBy = sort }, default)).Items.Count <= 25);
        foreach (var sort in new[] { "packages", "sold", "periodSold", "price", "periodPrice", "periodRevenue", "periodResult", "available", "revenue", "profit", "cost" })
            Assert.True((await repository.BalancesAsync(userId, query with { SortBy = sort }, default)).Items.Count <= 25);
        Assert.True((await repository.SalesAsync(userId, query with { SortBy = "total" }, default)).Items.Count <= 25);
        Assert.True((await repository.SummaryAsync(userId, new(), default)).Counts.Market >= 0);
        var production = (await repository.ProductionsAsync(userId, new(), default)).Items.FirstOrDefault();
        if (production is not null)
        {
            Assert.NotNull(await repository.ReportAsync(userId, production.Id, query, default));
            Assert.True((await repository.ConsumptionsAsync(userId, production.Id, query, default)).Items.Count <= 25);
            foreach (var sort in new[] { "unit", "quantity", "cost", "packageQty", "packagePrice" })
                Assert.True((await repository.ConsumptionsAsync(userId, production.Id, query with { SortBy = sort }, default)).Items.Count <= 25);
            Assert.True((await repository.SalesAsync(userId, query with { ProductionId = production.Id }, default)).Items.Count <= 25);
        }
        var recipe = (await repository.RecipesAsync(userId, new(), default)).Items.FirstOrDefault();
        if (recipe is not null)
            foreach (var sort in new[] { "name", "unit", "quantity" })
                Assert.True((await repository.IngredientsAsync(userId, recipe.Id, query with { SortBy = sort }, default)).Items.Count <= 25);
        Assert.Empty(db.ChangeTracker.Entries());
    }
}

public sealed class SqlServerReadOnlyFactAttribute : FactAttribute
{
    public SqlServerReadOnlyFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("VALORES_TEST_SQLSERVER_READONLY") != "1")
            Skip = "Opt-in read-only query verification against the configured SQL Server.";
    }
}

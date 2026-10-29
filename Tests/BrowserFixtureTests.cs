using Xunit;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Tests;

public sealed class BrowserFixtureTests
{
    [BrowserFixtureFact]
    public async Task ServeIsolatedPaginationFixture()
    {
        await using var host = await ApiTestHost.CreateAsync(browser: true);
        using var client = await host.CreateUserAsync("pagination@example.test");
        await PaginationTests.SeedAsync(host, "pagination@example.test", 113);
        await host.WithDatabaseAsync(async db =>
        {
            var production = await db.Productions.OrderBy(item => item.Id).FirstAsync();
            var market = await db.MarketItems.OrderBy(item => item.Id).Skip(1).Take(31).ToListAsync();
            production.ActualYield = 100;
            for (var index = 0; index < market.Count; index++)
            {
                var id = $"browser-extra-{index:D3}";
                db.Ingredients.Add(new Ingredient
                {
                    Id = id, UserId = production.UserId, RecipeId = production.RecipeId,
                    MarketItemId = market[index].Id, Quantity = index + 1,
                });
                db.Sales.Add(new Sale
                {
                    Id = id, UserId = production.UserId, ProductionId = production.Id,
                    RecipeName = production.RecipeName, ProductionDate = production.Date,
                    Date = production.Date.AddDays(index + 1), Packages = 1,
                    PricePerPackage = 5, CostPerPackage = 2, Paid = true, Customer = "", Notes = "",
                });
            }
            await db.SaveChangesAsync();
        });
        Console.WriteLine("BROWSER_FIXTURE_READY http://127.0.0.1:5232");
        await Task.Delay(TimeSpan.FromMinutes(10));
    }
}

public sealed class BrowserFixtureFactAttribute : FactAttribute
{
    public BrowserFixtureFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("VALORES_BROWSER_FIXTURE") != "1")
            Skip = "Opt-in temporary HTTP server with isolated SQLite data for browser validation.";
    }
}

using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Models;
using Xunit;

namespace ValoresReceitas.Api.Tests;

public sealed class SalesBrowserFixtureTests
{
    [BrowserFixtureFact]
    public async Task ServeIsolatedSalesDashboard()
    {
        await using var host = await ApiTestHost.CreateAsync(browser: true, browserPort: 5238);
        using var client = await host.CreateUserAsync("sales-dashboard@example.test");
        await PaginationTests.SeedAsync(host, "sales-dashboard@example.test", 3);
        await host.WithDatabaseAsync(async db =>
        {
            var productions = await db.Productions.OrderBy(item => item.Id).ToListAsync();
            var today = DateOnly.FromDateTime(DateTime.Today);
            foreach (var existing in await db.Sales.ToListAsync()) existing.IsDeleted = true;
            string[] products = ["Brownie de chocolate", "Cookie de baunilha", "Bolo de cenoura"];
            string[] customers = ["Ana Souza", "Bruno Lima", "Carla Santos", "Daniel Costa", "", "Eva Oliveira"];
            for (var index = 0; index < productions.Count; index++)
            {
                productions[index].RecipeName = products[index];
                productions[index].Date = today.AddDays(-120);
                productions[index].ActualYield = 300;
                productions[index].TotalCost = 600;
            }
            for (var index = 0; index < 90; index++)
            {
                var production = productions[index % productions.Count];
                db.Sales.Add(new Sale
                {
                    Id = $"dashboard-{index:D3}", UserId = production.UserId, ProductionId = production.Id,
                    RecipeName = production.RecipeName, ProductionDate = production.Date, Date = today.AddDays(-index),
                    Packages = 1 + index % 4, PricePerPackage = index % 17 == 0 ? 0 : 10 + index % 5,
                    CostPerPackage = 2, Paid = index % 17 == 0 || index % 5 != 0, Donated = index % 17 == 0,
                    Customer = customers[index % customers.Length], Notes = index % 5 == 0 ? "Pagamento na entrega" : "",
                });
            }
            await db.SaveChangesAsync();
        });
        Console.WriteLine("SALES_BROWSER_FIXTURE_READY http://127.0.0.1:5238");
        await Task.Delay(TimeSpan.FromMinutes(10));
    }
}

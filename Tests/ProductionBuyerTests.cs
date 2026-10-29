using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;
using Xunit;

namespace ValoresReceitas.Api.Tests;

public sealed class ProductionBuyerTests
{
    [Fact]
    public async Task RankingAggregatesAllPurchasesAndExcludesDonationsDeletedAndForeignSales()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("buyers@example.test");
        using var other = await host.CreateUserAsync("other-buyers@example.test");
        await PaginationTests.SeedAsync(host, "buyers@example.test", 2);
        var production = (await client.GetFromJsonAsync<PageDto<ProductionDto>>("/api/query/productions"))!.Items[0];
        await host.WithDatabaseAsync(async db =>
        {
            var userId = await db.Users.Where(user => user.Email == "buyers@example.test").Select(user => user.Id).SingleAsync();
            var otherId = await db.Users.Where(user => user.Email == "other-buyers@example.test").Select(user => user.Id).SingleAsync();
            var existing = await db.Sales.SingleAsync(sale => sale.ProductionId == production.Id);
            existing.IsDeleted = true;
            existing.Packages = 999;
            for (var index = 0; index < 11; index++)
                db.Sales.Add(Purchase(production.Id, userId, index % 2 == 0 ? " Ana " : "ana", 1, 1.5m,
                    date: new DateOnly(2026, 10, index + 2)));
            db.Sales.AddRange(
                Purchase(production.Id, userId, "Bruno", 10, 20, paid: false),
                Purchase(production.Id, userId, "Carla", 8, 4),
                Purchase(production.Id, userId, "Daniel", 8, 3),
                Purchase(production.Id, userId, "Eva", 8, 3),
                Purchase(production.Id, userId, "Fabio", 7, 100),
                Purchase(production.Id, userId, "Donatario", 1000, 0, donated: true),
                Purchase(production.Id, otherId, "Outro usuario", 1000, 100));
            await db.SaveChangesAsync();
        });

        var report = (await client.GetFromJsonAsync<ProductionReportDto>(
            $"/api/query/productions/{production.Id}/report?page=2&pageSize=10"))!;
        Assert.Equal(2, report.Daily.Page);
        Assert.Equal(5, report.TopBuyers.Count);
        Assert.Equal(new[] { "Ana", "Bruno", "Carla", "Daniel", "Eva" }, report.TopBuyers.Select(buyer => buyer.Customer));
        Assert.Equal(new[] { 11, 10, 8, 8, 8 }, report.TopBuyers.Select(buyer => buyer.Packages));
        Assert.Equal(new[] { 16.5, 200, 32, 24, 24 }, report.TopBuyers.Select(buyer => buyer.Spent));
        var firstPage = (await client.GetFromJsonAsync<ProductionReportDto>(
            $"/api/query/productions/{production.Id}/report"))!;
        Assert.Equal(firstPage.TopBuyers, report.TopBuyers);
        var balance = (await client.GetFromJsonAsync<ProductionBalanceDto>(
            $"/api/query/balances/{production.Id}"))!;
        Assert.Equal(1000, balance.DonatedPackages);
        Assert.Equal(1052, balance.SoldPackages);
        var summary = (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!;
        Assert.Equal(1000, summary.Productions.DonatedPackages);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.GetAsync($"/api/query/productions/{production.Id}/report")).StatusCode);
        using var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/query/productions/{production.Id}/report")).StatusCode);
    }

    [Fact]
    public async Task RankingHandlesNoPurchasesAndGroupsUnnamedBuyers()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("empty-buyers@example.test");
        await PaginationTests.SeedAsync(host, "empty-buyers@example.test", 1);
        var production = (await client.GetFromJsonAsync<PageDto<ProductionDto>>("/api/query/productions"))!.Items[0];
        await host.WithDatabaseAsync(async db =>
        {
            var sale = await db.Sales.SingleAsync(item => item.ProductionId == production.Id);
            sale.Donated = true;
            sale.PricePerPackage = 0;
            await db.SaveChangesAsync();
        });
        var empty = (await client.GetFromJsonAsync<ProductionReportDto>(
            $"/api/query/productions/{production.Id}/report"))!;
        Assert.Empty(empty.TopBuyers);
        await host.WithDatabaseAsync(async db =>
        {
            var userId = await db.Users.Select(user => user.Id).SingleAsync();
            db.Sales.AddRange(Purchase(production.Id, userId, "", 2, 1.25m),
                Purchase(production.Id, userId, "   ", 1, 2.5m));
            await db.SaveChangesAsync();
        });
        var report = (await client.GetFromJsonAsync<ProductionReportDto>(
            $"/api/query/productions/{production.Id}/report"))!;
        var buyer = Assert.Single(report.TopBuyers);
        Assert.Equal(new ProductionBuyerDto("", 3, 5), buyer);
    }

    private static Sale Purchase(string productionId, string userId, string customer, int packages,
        decimal price, bool paid = true, bool donated = false, DateOnly? date = null) => new()
    {
        Id = Guid.NewGuid().ToString(), UserId = userId, ProductionId = productionId,
        ProductionDate = new(2026, 10, 1), RecipeName = "Teste", Date = date ?? new DateOnly(2026, 10, 2),
        Packages = packages, PricePerPackage = price, CostPerPackage = 1, Customer = customer,
        Paid = paid, Donated = donated, Notes = "",
    };
}

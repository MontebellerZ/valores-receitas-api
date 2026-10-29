using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;
using Xunit;

namespace ValoresReceitas.Api.Tests;

public sealed class SalesReportTests
{
    [Fact]
    public async Task ReportAggregatesAllPagesAndSeparatesDonationsPaymentsAndOwners()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("sales-report@example.test");
        using var other = await host.CreateUserAsync("foreign-report@example.test");
        await PaginationTests.SeedAsync(host, "sales-report@example.test", 2);
        var productions = (await client.GetFromJsonAsync<PageDto<ProductionDto>>("/api/query/productions"))!.Items;
        var first = productions[0].Id;
        var second = productions[1].Id;
        await host.WithDatabaseAsync(async db =>
        {
            var userId = await db.Users.Where(user => user.Email == "sales-report@example.test").Select(user => user.Id).SingleAsync();
            var foreignId = await db.Users.Where(user => user.Email == "foreign-report@example.test").Select(user => user.Id).SingleAsync();
            foreach (var sale in await db.Sales.Where(sale => sale.UserId == userId).ToListAsync()) sale.IsDeleted = true;
            for (var day = 1; day <= 31; day++)
                db.Sales.Add(Purchase(first, userId, day % 2 == 0 ? " Ana " : "ana", 1, 10, new(2026, 10, day), paid: day != 31));
            db.Sales.AddRange(
                Purchase(second, userId, "Bruno", 2, 20, new(2026, 10, 31), paid: false, product: "Bolo"),
                Purchase(first, userId, "Doação", 3, 0, new(2026, 10, 2), paid: false, donated: true),
                Purchase(first, userId, "Fora do período", 100, 100, new(2026, 9, 30)),
                Purchase(first, foreignId, "Outro usuário", 100, 100, new(2026, 10, 2)));
            await db.SaveChangesAsync();
        });
        const string period = "from=2026-10-01&to=2026-10-31";
        using var response = await client.GetAsync($"/api/query/sales-report?{period}&page=2&pageSize=10");
        response.EnsureSuccessStatusCode();
        var report = (await response.Content.ReadFromJsonAsync<SalesReportDto>())!;
        Assert.Equal(new SalesReportTotalsDto(32, 33, 3, 2, 2, 350, 300, 50, 66, 6), report.Totals);
        Assert.Equal(31, report.Daily.TotalCount);
        Assert.Equal(10, report.Daily.Items.Count);
        Assert.Equal(new DateOnly(2026, 10, 11), report.Daily.Items[0].Date);
        Assert.Equal(new[] { "Cookie", "Bolo" }, report.TopProducts.Select(product => product.Name));
        Assert.Equal(310, report.TopProducts[0].Revenue);
        Assert.Equal(31, report.TopBuyers[0].Packages);
        Assert.Equal(310, report.TopBuyers[0].Spent);
        var firstPage = (await client.GetFromJsonAsync<SalesReportDto>($"/api/query/sales-report?{period}"))!;
        Assert.Equal(firstPage.Totals, report.Totals);
        Assert.Equal(firstPage.TopProducts, report.TopProducts);
        Assert.Equal(firstPage.TopBuyers, report.TopBuyers);
        var filtered = (await client.GetFromJsonAsync<SalesReportDto>(
            $"/api/query/sales-report?{period}&productionId={second}&search=missing&payment=donated"))!;
        Assert.Equal(new SalesReportTotalsDto(1, 2, 0, 1, 1, 40, 0, 40, 4, 0), filtered.Totals);
        Assert.Single(filtered.Daily.Items);
        var foreign = (await other.GetFromJsonAsync<SalesReportDto>(
            $"/api/query/sales-report?{period}&productionId={second}"))!;
        Assert.Equal(0, foreign.Totals.Count);
        Assert.Empty(foreign.TopProducts);
        foreach (var (payment, expected) in new[] { ("paid", 30), ("pending", 2), ("donated", 1) })
        {
            var page = (await client.GetFromJsonAsync<PageDto<SaleDto>>($"/api/query/sales?{period}&payment={payment}"))!;
            Assert.Equal(expected, page.TotalCount);
            Assert.All(page.Items, sale => Assert.Equal(payment == "donated", sale.Donated));
        }
        var lastDay = (await client.GetFromJsonAsync<SalesReportDto>(
            "/api/query/sales-report?from=2026-10-31&to=2026-10-31"))!;
        Assert.Equal(50, lastDay.Totals.Revenue);
    }

    [Fact]
    public async Task RankingsAreBoundedAndUnnamedBuyersDoNotInflateCustomerCounts()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("unnamed-sales@example.test");
        await PaginationTests.SeedAsync(host, "unnamed-sales@example.test", 1);
        var production = (await client.GetFromJsonAsync<PageDto<ProductionDto>>("/api/query/productions"))!.Items[0];
        await host.WithDatabaseAsync(async db =>
        {
            var userId = await db.Users.Select(user => user.Id).SingleAsync();
            (await db.Sales.SingleAsync()).IsDeleted = true;
            for (var index = 1; index <= 7; index++)
                db.Sales.Add(Purchase(production.Id, userId, index % 2 == 0 ? "" : "   ", 1, index,
                    new(2026, 10, 1), product: $"Produto {index}"));
            db.Sales.Add(Purchase(production.Id, userId, "Doação", 2, 0, new(2026, 10, 1), paid: false, donated: true));
            await db.SaveChangesAsync();
        });
        var report = (await client.GetFromJsonAsync<SalesReportDto>("/api/query/sales-report"))!;
        Assert.Equal(0, report.Totals.Customers);
        Assert.Equal(7, report.Totals.Count);
        Assert.Equal(0, report.Totals.PendingCount);
        Assert.Equal(new[] { "Produto 7", "Produto 6", "Produto 5", "Produto 4", "Produto 3" },
            report.TopProducts.Select(product => product.Name));
        Assert.Equal(new ProductionBuyerDto("", 7, 28), Assert.Single(report.TopBuyers));
        await host.WithDatabaseAsync(async db =>
        {
            foreach (var sale in await db.Sales.Where(sale => !sale.Donated).ToListAsync()) sale.IsDeleted = true;
            await db.SaveChangesAsync();
        });
        var donations = (await client.GetFromJsonAsync<SalesReportDto>("/api/query/sales-report"))!;
        Assert.Equal(new SalesReportTotalsDto(0, 0, 2, 0, 0, 0, 0, 0, 0, 4), donations.Totals);
        Assert.Empty(donations.Daily.Items);
        Assert.Empty(donations.TopProducts);
        Assert.Empty(donations.TopBuyers);
    }

    [Fact]
    public async Task EmptyReportAndInvalidQueriesHaveExplicitContracts()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/query/sales-report")).StatusCode);
        using var client = await host.CreateUserAsync("empty-sales-report@example.test");
        var empty = (await client.GetFromJsonAsync<SalesReportDto>("/api/query/sales-report"))!;
        Assert.Equal(new SalesReportTotalsDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0), empty.Totals);
        Assert.Empty(empty.Daily.Items);
        Assert.Empty(empty.TopProducts);
        Assert.Empty(empty.TopBuyers);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/query/sales-report?from=2026-10-31&to=2026-10-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/query/sales?payment=invalid")).StatusCode);
    }

    private static Sale Purchase(string productionId, string userId, string customer, int packages,
        decimal price, DateOnly date, bool paid = true, bool donated = false, string product = "Cookie") => new()
    {
        Id = Guid.NewGuid().ToString(), UserId = userId, ProductionId = productionId,
        ProductionDate = new(2026, 9, 1), RecipeName = product, Date = date,
        Packages = packages, PricePerPackage = price, CostPerPackage = 2,
        Customer = customer, Paid = paid, Donated = donated, Notes = "",
    };
}

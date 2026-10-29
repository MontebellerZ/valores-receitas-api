using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;
using Xunit;

namespace ValoresReceitas.Api.Tests;

public sealed class PaginationTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task PagesAreBoundedStableFilteredAndOwned(int size)
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var owner = await host.CreateUserAsync("paging@example.test");
        using var other = await host.CreateUserAsync("other@example.test");
        await SeedAsync(host, "paging@example.test", 113);
        await SeedAsync(host, "other@example.test", 5);
        var first = (await owner.GetFromJsonAsync<PageDto<MarketItemDto>>($"/api/market?pageSize={size}"))!;
        var second = (await owner.GetFromJsonAsync<PageDto<MarketItemDto>>($"/api/query/market?page=2&pageSize={size}"))!;
        Assert.Equal(113, first.TotalCount);
        Assert.Equal(size, first.Items.Count);
        Assert.Equal(Math.Min(size, 113 - size), second.Items.Count);
        Assert.Equal((int)Math.Ceiling(113d / size), first.TotalPages);
        Assert.Empty(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)));
        var filtered = (await owner.GetFromJsonAsync<PageDto<MarketItemDto>>("/api/query/market?search=Item%2011"))!;
        Assert.Equal(3, filtered.TotalCount);
        Assert.All(filtered.Items, item => Assert.Contains("Item 11", item.Name));
        var empty = (await owner.GetFromJsonAsync<PageDto<MarketItemDto>>("/api/query/market?page=999"))!;
        Assert.Empty(empty.Items);
        Assert.Equal(113, empty.TotalCount);
        var foreignId = (await other.GetFromJsonAsync<PageDto<MarketItemDto>>("/api/query/market"))!.Items[0].Id;
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/query/market/{foreignId}")).StatusCode);
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=11")]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("page=2147483647")]
    [InlineData("sortBy=not-a-field")]
    [InlineData("from=2026-10-10&to=2026-01-01")]
    public async Task InvalidQueriesReturnBadRequest(string query)
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("invalid@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/query/market?{query}")).StatusCode);
    }

    [Fact]
    public async Task AllResourcesArePagedAndAggregatesUseRecordsBeyondThePage()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("resources@example.test");
        await SeedAsync(host, "resources@example.test", 31);
        var recipes = (await client.GetFromJsonAsync<PageDto<RecipeDto>>("/api/query/recipes"))!;
        var productions = (await client.GetFromJsonAsync<PageDto<ProductionDto>>("/api/query/productions"))!;
        var sales = (await client.GetFromJsonAsync<PageDto<SaleDto>>("/api/query/sales"))!;
        var stock = (await client.GetFromJsonAsync<PageDto<StockLotDto>>("/api/query/stock"))!;
        var balances = (await client.GetFromJsonAsync<PageDto<ProductionBalanceDto>>("/api/query/balances"))!;
        var stockTotals = (await client.GetFromJsonAsync<PageDto<StockTotalDto>>("/api/query/stock-totals"))!;
        foreach (var value in new[] { recipes.Items.Count, productions.Items.Count, sales.Items.Count,
                     stock.Items.Count, balances.Items.Count, stockTotals.Items.Count })
            Assert.Equal(10, value);
        Assert.Equal(31, sales.TotalCount);
        Assert.All(balances.Items, item =>
        {
            Assert.Equal(1, item.SoldPackages);
            Assert.Equal(10, item.Packages);
            Assert.Equal(8, item.AvailablePackages);
        });
        var summary = (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!;
        Assert.Equal(31, summary.Counts.Productions);
        Assert.Equal(31, summary.Counts.Sales);
        Assert.Equal(31 * 8, summary.AvailablePackages);
        Assert.Equal(31 * 5, summary.Sales.Revenue);
        Assert.Equal(31 * 2, summary.Sales.Cost);
        Assert.Equal(31 * 20, summary.ProductionCost);
        Assert.Equal(31, summary.Sales.Count);
        Assert.Equal(31, summary.Productions.SoldPackages);
        Assert.Equal(31 * 8 * 2, summary.Productions.RemainingCost);
        Assert.Equal(31 * 8 * 2, summary.Productions.RemainingRevenue);
        var recipeId = recipes.Items[0].Id;
        var ingredients = (await client.GetFromJsonAsync<PageDto<IngredientDto>>($"/api/query/recipes/{recipeId}/ingredients"))!;
        Assert.Single(ingredients.Items);
        Assert.Empty(recipes.Items[0].Ingredients);
        Assert.Equal(1, recipes.Items[0].IngredientCount);
        Assert.Single((await client.GetFromJsonAsync<RecipeDto>($"/api/query/recipes/{recipeId}"))!.Ingredients);
        var productionId = productions.Items[0].Id;
        var consumption = (await client.GetFromJsonAsync<PageDto<StockConsumptionDto>>($"/api/query/productions/{productionId}/consumptions"))!;
        Assert.Empty(consumption.Items);
        var filtered = (await client.GetFromJsonAsync<PageDto<SaleDto>>($"/api/query/sales?productionId={productionId}"))!;
        Assert.Single(filtered.Items);
        var past = (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary?to=2020-01-01"))!;
        Assert.Equal(0, past.Sales.Revenue);
        Assert.Equal(0, past.ProductionCost);
        Assert.Equal(31, past.Counts.Sales);
        var balance = (await client.GetFromJsonAsync<ProductionBalanceDto>($"/api/query/balances/{productionId}"))!;
        Assert.Equal(1, balance.PeriodSales.Count);
        Assert.Equal(5, balance.PeriodSales.Revenue);
        var available = (await client.GetFromJsonAsync<PageDto<ProductionBalanceDto>>("/api/query/balances?available=true"))!;
        Assert.Equal(31, available.TotalCount);
        var report = (await client.GetFromJsonAsync<ProductionReportDto>($"/api/query/productions/{productionId}/report"))!;
        Assert.Equal(1, report.SalesCount);
        Assert.Equal(1, report.OutPackages);
        Assert.Equal(5, report.Revenue);
        Assert.Equal(2, report.SoldCost);
        Assert.Single(report.Daily.Items);
        Assert.Equal(new DateOnly(2026, 10, 2), report.FirstSaleDate);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/query/productions/foreign/report")).StatusCode);
        var lookup = (await client.GetFromJsonAsync<PageDto<MarketItemDto>>($"/api/query/market?ids={recipeId}"))!;
        Assert.Single(lookup.Items);
        Assert.Equal(1, lookup.Items[0].IngredientCount);
    }

    [Fact]
    public async Task PatchPreservesUnloadedRecordsAndRejectsStaleRevision()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("patch@example.test");
        await SeedAsync(host, "patch@example.test", 31);
        var before = (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!;
        var first = (await client.GetFromJsonAsync<PageDto<MarketItemDto>>("/api/query/market"))!.Items[0];
        var patch = new WorkspacePatchDto([first with { Name = "Updated" }], [], [], [],
            new([], [], [], []), before.Revision);
        host.CommandRecorder.Clear();
        var response = await client.PatchAsJsonAsync("/api/data", patch);
        var patchCommands = host.CommandRecorder.Commands;
        response.EnsureSuccessStatusCode();
        Assert.DoesNotContain(patchCommands, command =>
            command.Contains("\"Recipes\"", StringComparison.Ordinal) ||
            command.Contains("\"Ingredients\"", StringComparison.Ordinal) ||
            command.Contains("\"Productions\"", StringComparison.Ordinal) ||
            command.Contains("\"Sales\"", StringComparison.Ordinal));
        var saved = (await response.Content.ReadFromJsonAsync<DataSnapshotDto>())!;
        Assert.Single(saved.Market);
        Assert.Empty(saved.Recipes);
        Assert.Empty(saved.Productions);
        Assert.Empty(saved.Sales);
        Assert.Equal(before.Revision + 1, saved.Revision);
        Assert.True(saved.Market[0].HasStockHistory);
        var after = (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!;
        Assert.Equal(before.Counts, after.Counts);
        Assert.Equal("Updated", (await client.GetFromJsonAsync<MarketItemDto>($"/api/query/market/{first.Id}"))!.Name);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync("/api/data", patch)).StatusCode);
        Assert.Equal("Updated", (await client.GetFromJsonAsync<MarketItemDto>($"/api/query/market/{first.Id}"))!.Name);
    }

    [Fact]
    public async Task EmptyPatchReturnsNoChangedRecordsAndLeavesWorkspaceDataUnchanged()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("empty-patch@example.test");
        await SeedAsync(host, "empty-patch@example.test", 1);
        var before = (await client.GetFromJsonAsync<DataSnapshotDto>("/api/data"))!;
        var summary = (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!;
        var patch = new WorkspacePatchDto([], [], [], [], new([], [], [], []), summary.Revision);

        host.CommandRecorder.Clear();
        using var response = await client.PatchAsJsonAsync("/api/data", patch);
        var patchCommands = host.CommandRecorder.Commands;
        response.EnsureSuccessStatusCode();
        var saved = (await response.Content.ReadFromJsonAsync<DataSnapshotDto>())!;

        Assert.DoesNotContain(patchCommands, command =>
            command.Contains("\"MarketItems\"", StringComparison.Ordinal) ||
            command.Contains("\"Recipes\"", StringComparison.Ordinal) ||
            command.Contains("\"Ingredients\"", StringComparison.Ordinal) ||
            command.Contains("\"Productions\"", StringComparison.Ordinal) ||
            command.Contains("\"Sales\"", StringComparison.Ordinal) ||
            command.Contains("\"StockLots\"", StringComparison.Ordinal));
        Assert.Empty(saved.Market);
        Assert.Empty(saved.Recipes);
        Assert.Empty(saved.Productions);
        Assert.Empty(saved.Sales);
        Assert.Equal(summary.Revision + 1, saved.Revision);
        var after = (await client.GetFromJsonAsync<DataSnapshotDto>("/api/data"))!;
        Assert.Equal(before.Market.Select(item => item.Id), after.Market.Select(item => item.Id));
        Assert.Equal(before.Recipes.Select(item => item.Id), after.Recipes.Select(item => item.Id));
        Assert.Equal(before.Productions.Select(item => item.Id), after.Productions.Select(item => item.Id));
        Assert.Equal(before.Sales.Select(item => item.Id), after.Sales.Select(item => item.Id));
    }

    [Fact]
    public async Task MarketOnlyPatchRejectsUnitChangesForItemsWithStockHistory()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("patch-stock-history@example.test");
        await SeedAsync(host, "patch-stock-history@example.test", 1);
        var item = (await client.GetFromJsonAsync<PageDto<MarketItemDto>>("/api/query/market"))!.Items.Single();
        var summary = (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!;
        var patch = new WorkspacePatchDto(
            [item with { Unit = "g" }], [], [], [], new([], [], [], []), summary.Revision);

        using var response = await client.PatchAsJsonAsync("/api/data", patch);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(summary.Revision, (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!.Revision);
        Assert.Equal("kg", (await client.GetFromJsonAsync<MarketItemDto>($"/api/query/market/{item.Id}"))!.Unit);
    }

    [Fact]
    public async Task SaleOnlyPatchUpdatesOneSaleWithoutReadingCatalogTables()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("sale-patch@example.test");
        await SeedAsync(host, "sale-patch@example.test", 1);
        var sale = (await client.GetFromJsonAsync<PageDto<SaleDto>>("/api/query/sales"))!.Items.Single();
        var summary = (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!;
        var patch = new WorkspacePatchDto([], [], [], [sale with { PricePerPackage = 7 }],
            new([], [], [], []), summary.Revision);

        host.CommandRecorder.Clear();
        using var response = await client.PatchAsJsonAsync("/api/data", patch);
        var patchCommands = host.CommandRecorder.Commands;
        response.EnsureSuccessStatusCode();
        var saved = (await response.Content.ReadFromJsonAsync<DataSnapshotDto>())!;

        Assert.Single(saved.Sales);
        Assert.Equal(7, saved.Sales[0].PricePerPackage);
        Assert.Equal(sale.CostPerPackage, saved.Sales[0].CostPerPackage);
        Assert.DoesNotContain(patchCommands, command =>
            command.Contains("\"MarketItems\"", StringComparison.Ordinal) ||
            command.Contains("\"Recipes\"", StringComparison.Ordinal) ||
            command.Contains("\"Ingredients\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaleOnlyPatchUsesAggregatedExistingPackagesForCapacityValidation()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("sale-capacity-patch@example.test");
        await SeedAsync(host, "sale-capacity-patch@example.test", 1);
        var sale = (await client.GetFromJsonAsync<PageDto<SaleDto>>("/api/query/sales"))!.Items.Single();
        await host.WithDatabaseAsync(async db =>
        {
            var userId = await db.Users.Where(user => user.Email == "sale-capacity-patch@example.test")
                .Select(user => user.Id).SingleAsync();
            db.Sales.AddRange(Enumerable.Range(0, 8).Select(index => new Sale
            {
                Id = $"additional-sale-{index}", UserId = userId, ProductionId = sale.ProductionId,
                ProductionDate = sale.ProductionDate, RecipeName = sale.RecipeName, Date = sale.Date,
                Packages = 1, PricePerPackage = 5, CostPerPackage = sale.CostPerPackage,
                Customer = "Cliente", Paid = true, Notes = "",
            }));
            await db.SaveChangesAsync();
        });
        var summary = (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!;
        var patch = new WorkspacePatchDto([], [], [], [sale with { Packages = 2 }],
            new([], [], [], []), summary.Revision);

        host.CommandRecorder.Clear();
        using var response = await client.PatchAsJsonAsync("/api/data", patch);
        var patchCommands = host.CommandRecorder.Commands;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(patchCommands, command =>
            command.Contains("\"Sales\"", StringComparison.Ordinal) &&
            command.Contains("GROUP BY", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(summary.Revision, (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!.Revision);
        Assert.Equal(1, (await client.GetFromJsonAsync<SaleDto>($"/api/query/sales/{sale.Id}"))!.Packages);
    }

    [Fact]
    public async Task NewProductionAndSalePatchConsumesStockOnceAndUsesScopedReads()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("new-production-patch@example.test");
        await SeedAsync(host, "new-production-patch@example.test", 1);
        var recipe = (await client.GetFromJsonAsync<PageDto<RecipeDto>>("/api/query/recipes"))!.Items.Single();
        var summary = (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!;
        var productionId = "new-production";
        var productionDate = new DateOnly(2026, 10, 3);
        var production = new ProductionDto(productionId, productionDate, recipe.Id, recipe.Name, 1, 10, 0,
            0, 1, recipe.Yield, 0, null);
        var sale = new SaleDto("new-sale", productionId, productionDate, recipe.Name,
            productionDate.AddDays(1), 1, 5, 0, "Cliente", true, "");
        var patch = new WorkspacePatchDto([], [], [production], [sale],
            new([], [], [], []), summary.Revision);

        host.CommandRecorder.Clear();
        using var response = await client.PatchAsJsonAsync("/api/data", patch);
        var patchCommands = host.CommandRecorder.Commands;
        response.EnsureSuccessStatusCode();
        var saved = (await response.Content.ReadFromJsonAsync<DataSnapshotDto>())!;

        Assert.Single(saved.Productions);
        Assert.Equal(2, saved.Productions[0].TotalCost);
        Assert.Single(saved.Sales);
        Assert.Equal(0.2m, saved.Sales[0].CostPerPackage);
        Assert.DoesNotContain(patchCommands, command => command.Contains("\"MarketItems\"", StringComparison.Ordinal));
        await host.WithDatabaseAsync(async db =>
        {
            var userId = await db.Users.Where(user => user.Email == "new-production-patch@example.test")
                .Select(user => user.Id).SingleAsync();
            Assert.Equal(4, await db.StockLots.Where(lot => lot.UserId == userId).Select(lot => lot.RemainingQty).SingleAsync());
            Assert.Single(await db.StockConsumptions.Where(item => item.ProductionId == productionId).ToListAsync());
        });
    }

    [Fact]
    public async Task ProductionOnlyPatchRefreshesRelatedSaleMetadataWithoutReadingCatalogTables()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("production-patch@example.test");
        await SeedAsync(host, "production-patch@example.test", 1);
        var production = (await client.GetFromJsonAsync<PageDto<ProductionDto>>("/api/query/productions"))!.Items.Single();
        var summary = (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!;
        var patch = new WorkspacePatchDto([], [], [production with { Date = production.Date.AddDays(1) }],
            [], new([], [], [], []), summary.Revision);

        host.CommandRecorder.Clear();
        using var response = await client.PatchAsJsonAsync("/api/data", patch);
        var patchCommands = host.CommandRecorder.Commands;
        response.EnsureSuccessStatusCode();
        var saved = (await response.Content.ReadFromJsonAsync<DataSnapshotDto>())!;

        Assert.Single(saved.Productions);
        Assert.Empty(saved.Sales);
        var sale = (await client.GetFromJsonAsync<PageDto<SaleDto>>("/api/query/sales"))!.Items.Single();
        Assert.Equal(production.Date.AddDays(1), sale.ProductionDate);
        Assert.DoesNotContain(patchCommands, command =>
            command.Contains("\"MarketItems\"", StringComparison.Ordinal) ||
            command.Contains("\"Recipes\"", StringComparison.Ordinal) ||
            command.Contains("\"Ingredients\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FailedNewProductionPatchRollsBackStockAndRevision()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("failed-production-patch@example.test");
        await SeedAsync(host, "failed-production-patch@example.test", 1);
        var recipe = (await client.GetFromJsonAsync<PageDto<RecipeDto>>("/api/query/recipes"))!.Items.Single();
        var summary = (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!;
        var production = new ProductionDto("insufficient-production", new DateOnly(2026, 10, 3),
            recipe.Id, recipe.Name, 10, 100, 0, 0, 1, recipe.Yield, 0, null);
        var patch = new WorkspacePatchDto([], [], [production], [], new([], [], [], []), summary.Revision);

        using var response = await client.PatchAsJsonAsync("/api/data", patch);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(summary.Revision, (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!.Revision);
        Assert.Equal(1, (await client.GetFromJsonAsync<PageDto<ProductionDto>>("/api/query/productions"))!.TotalCount);
        await host.WithDatabaseAsync(async db =>
        {
            var userId = await db.Users.Where(user => user.Email == "failed-production-patch@example.test")
                .Select(user => user.Id).SingleAsync();
            Assert.Equal(5, await db.StockLots.Where(lot => lot.UserId == userId).Select(lot => lot.RemainingQty).SingleAsync());
            Assert.Empty(await db.StockConsumptions.Where(item => item.ProductionId == production.Id).ToListAsync());
        });
    }

    [Fact]
    public async Task SavedProductionPatchCannotChangeItsRecipeOrQuantity()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("immutable-production-patch@example.test");
        await SeedAsync(host, "immutable-production-patch@example.test", 1);
        var production = (await client.GetFromJsonAsync<PageDto<ProductionDto>>("/api/query/productions"))!.Items.Single();
        var summary = (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!;
        var patch = new WorkspacePatchDto([], [], [production with { Quantity = 2 }],
            [], new([], [], [], []), summary.Revision);

        using var response = await client.PatchAsJsonAsync("/api/data", patch);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(summary.Revision, (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!.Revision);
        Assert.Equal(1, (await client.GetFromJsonAsync<PageDto<ProductionDto>>("/api/query/productions"))!.Items.Single().Quantity);
    }

    [Fact]
    public async Task DailyReportAndNestedCollectionsAreBounded()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("nested@example.test");
        await SeedAsync(host, "nested@example.test", 1);
        var production = (await client.GetFromJsonAsync<PageDto<ProductionDto>>("/api/query/productions"))!.Items[0];
        await host.WithDatabaseAsync(async db =>
        {
            var userId = await db.Users.Where(user => user.Email == "nested@example.test").Select(user => user.Id).SingleAsync();
            for (var index = 0; index < 30; index++)
            {
                var id = $"extra-{index}";
                db.Sales.Add(new Sale
                {
                    Id = id, UserId = userId, ProductionId = production.Id, RecipeName = "Recipe",
                    ProductionDate = production.Date, Date = new DateOnly(2026, 10, 3).AddDays(index),
                    Packages = 1, PricePerPackage = 3, CostPerPackage = 2,
                    Customer = "", Notes = "", Paid = index % 2 == 0, Donated = false,
                });
                db.MarketItems.Add(new MarketItem { Id = id, UserId = userId, Name = id, Unit = "kg" });
                db.Ingredients.Add(new Ingredient { Id = id, UserId = userId, RecipeId = production.RecipeId, MarketItemId = id, Quantity = 1 });
                db.StockLots.Add(new StockLot
                {
                    Id = id, UserId = userId, MarketItemId = production.Id, ItemName = "Item", Unit = "kg",
                    EntryDate = production.Date, EntryOrder = 99, PackageQty = 5, PackagePrice = 10,
                    RemainingQty = 5, IsActive = true,
                });
                db.StockConsumptions.Add(new StockConsumption
                {
                    Id = id, ProductionId = production.Id, StockLotId = id,
                    Quantity = index + 1, Cost = index + 2,
                });
            }
            await db.SaveChangesAsync();
        });
        var report = (await client.GetFromJsonAsync<ProductionReportDto>($"/api/query/productions/{production.Id}/report?page=2&pageSize=10"))!;
        Assert.Equal(31, report.SalesCount);
        Assert.Equal(31, report.Daily.TotalCount);
        Assert.Equal(10, report.Daily.Items.Count);
        Assert.Equal(95, report.Revenue);
        Assert.Equal(35, report.Daily.Items[0].CumulativeRevenue);
        Assert.Equal(62, report.Daily.Items[^1].CumulativeRevenue);
        var salePage = (await client.GetFromJsonAsync<PageDto<SaleDto>>(
            $"/api/query/sales?productionId={production.Id}&page=2&pageSize=10&sortBy=date"))!;
        Assert.Equal(35, salePage.Items[0].CumulativeRevenue);
        Assert.Equal(62, salePage.Items[^1].CumulativeRevenue);
        Assert.Equal(45, report.Pending);
        Assert.Equal(62, report.SoldCost);
        var ingredients = (await client.GetFromJsonAsync<PageDto<IngredientDto>>($"/api/recipes/{production.RecipeId}/ingredients"))!;
        Assert.Equal(31, ingredients.TotalCount);
        Assert.Equal(10, ingredients.Items.Count);
        var consumption = (await client.GetFromJsonAsync<PageDto<StockConsumptionDto>>($"/api/productions/{production.Id}/consumptions"))!;
        Assert.Equal(30, consumption.TotalCount);
        Assert.Equal(10, consumption.Items.Count);
        foreach (var sort in new[] { "quantity", "cost" })
        {
            var sorted = (await client.GetFromJsonAsync<PageDto<StockConsumptionDto>>(
                $"/api/query/productions/{production.Id}/consumptions?sortBy={sort}&descending=true"))!;
            Assert.Equal(30, sorted.Items[0].Quantity);
            Assert.Equal(31, sorted.Items[0].Cost);
            Assert.Equal(10, sorted.Items.Count);
        }
        var stock = (await client.GetFromJsonAsync<PageDto<StockLotDto>>("/api/query/stock?entryOrder=99&includeInactive=true"))!;
        Assert.Equal(30, stock.TotalCount);
        Assert.All(stock.Items, item => Assert.Equal(30, item.PurchaseLotCount));
        var unavailable = (await client.GetFromJsonAsync<PageDto<ProductionBalanceDto>>("/api/query/balances?available=true"))!;
        Assert.Empty(unavailable.Items);
    }

    [Fact]
    public async Task PartialIngredientEditsPreserveUnloadedIngredientsAndReturnOnlyChanges()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("ingredients@example.test");
        await SeedAsync(host, "ingredients@example.test", 31);
        var recipe = (await client.GetFromJsonAsync<PageDto<RecipeDto>>("/api/query/recipes"))!.Items[0];
        await host.WithDatabaseAsync(async db =>
        {
            var userId = await db.Users.Where(user => user.Email == "ingredients@example.test").Select(user => user.Id).SingleAsync();
            var otherItems = await db.MarketItems.Where(item => item.UserId == userId && item.Id != recipe.Id)
                .OrderBy(item => item.Id).ToListAsync();
            db.Ingredients.AddRange(otherItems.Select(item => new Ingredient
            {
                Id = $"added-{item.Id}", UserId = userId, RecipeId = recipe.Id, MarketItemId = item.Id, Quantity = 1,
            }));
            await db.SaveChangesAsync();
        });
        var metadata = (await client.GetFromJsonAsync<RecipeDto>($"/api/query/recipes/{recipe.Id}/metadata"))!;
        Assert.Empty(metadata.Ingredients);
        Assert.Equal(31, metadata.IngredientCount);
        var page = (await client.GetFromJsonAsync<PageDto<IngredientDto>>(
            $"/api/query/recipes/{recipe.Id}/ingredients?sortBy=name"))!;
        Assert.Equal(10, page.Items.Count);
        var summary = (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!;
        var patch = new WorkspacePatchDto([], [metadata with { Name = "Renamed", Ingredients = [page.Items[0] with { Quantity = 7 }] }],
            [], [], new([], [], [], []), summary.Revision, PartialRecipeIngredients: true,
            DeletedIngredients: [page.Items[1].Id]);
        host.CommandRecorder.Clear();
        using var response = await client.PatchAsJsonAsync("/api/data", patch);
        var patchCommands = host.CommandRecorder.Commands;
        response.EnsureSuccessStatusCode();
        Assert.DoesNotContain(patchCommands, command =>
            command.Contains("\"Productions\"", StringComparison.Ordinal) ||
            command.Contains("\"Sales\"", StringComparison.Ordinal));
        var saved = (await response.Content.ReadFromJsonAsync<DataSnapshotDto>())!;
        Assert.Single(saved.Recipes[0].Ingredients);
        Assert.Equal(7, saved.Recipes[0].Ingredients[0].Quantity);
        Assert.Equal(30, saved.Recipes[0].IngredientCount);
        var all = await client.GetPageItemsAsync<IngredientDto>($"/api/query/recipes/{recipe.Id}/ingredients");
        Assert.Equal(30, all.Count);
        Assert.DoesNotContain(all, item => item.Id == page.Items[1].Id);
        Assert.Equal(29, all.Count(item => item.Quantity == 1));
        var metadataOnly = patch with
        {
            Revision = saved.Revision!.Value, Recipes = [metadata with { Name = "Metadata only", Ingredients = [] }],
            DeletedIngredients = [],
        };
        (await client.PatchAsJsonAsync("/api/data", metadataOnly)).EnsureSuccessStatusCode();
        Assert.Equal(30, (await client.GetPageItemsAsync<IngredientDto>($"/api/query/recipes/{recipe.Id}/ingredients")).Count);
        var invalid = metadataOnly with
        {
            Revision = saved.Revision.Value + 1, DeletedIngredients = ["foreign"],
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync("/api/data", invalid)).StatusCode);
        var conflicting = invalid with
        {
            Recipes = patch.Recipes, DeletedIngredients = [page.Items[0].Id],
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync("/api/data", conflicting)).StatusCode);
        var missingMode = conflicting with { PartialRecipeIngredients = false };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync("/api/data", missingMode)).StatusCode);
        Assert.Equal(invalid.Revision, (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!.Revision);
    }

    [Fact]
    public async Task ComputedSortsUseAllRecordsBeforeTakingAPage()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("sorting@example.test");
        await SeedAsync(host, "sorting@example.test", 31);
        var target = (await client.GetFromJsonAsync<PageDto<ProductionDto>>("/api/query/productions?page=4"))!.Items.Single();
        await host.WithDatabaseAsync(async db =>
        {
            var production = await db.Productions.SingleAsync(item => item.Id == target.Id);
            production.ActualYield = 100;
            production.TotalCost = 200;
            production.Margin = 200;
            var sale = await db.Sales.SingleAsync(item => item.ProductionId == target.Id);
            sale.Packages = 5;
            sale.PricePerPackage = 20;
            await db.SaveChangesAsync();
        });
        foreach (var sort in new[] { "packages", "cost", "profit" })
        {
            var page = (await client.GetFromJsonAsync<PageDto<ProductionDto>>($"/api/query/productions?sortBy={sort}&descending=true"))!;
            Assert.Equal(target.Id, page.Items[0].Id);
            Assert.Equal(10, page.Items.Count);
        }
        foreach (var sort in new[] { "packages", "sold", "periodSold", "price", "periodPrice", "periodRevenue", "periodResult", "available", "revenue", "profit", "cost" })
        {
            var page = (await client.GetFromJsonAsync<PageDto<ProductionBalanceDto>>($"/api/query/balances?sortBy={sort}&descending=true"))!;
            Assert.Equal(target.Id, page.Items[0].Production.Id);
        }
        var sales = (await client.GetFromJsonAsync<PageDto<SaleDto>>("/api/query/sales?sortBy=total&descending=true"))!;
        Assert.Equal(target.Id, sales.Items[0].ProductionId);
        foreach (var sort in new[] { "periodPrice", "periodRevenue", "periodResult" })
        {
            var noSales = (await client.GetFromJsonAsync<PageDto<ProductionBalanceDto>>(
                $"/api/query/balances?sortBy={sort}&descending=true&to=2026-10-01"))!;
            Assert.Equal(target.Id, noSales.Items[0].Production.Id);
            Assert.Equal(0, noSales.Items[0].PeriodSales.Count);
        }
    }

    [Fact]
    public async Task PatchDeletionIsExplicitAndForeignIdsDoNotMutateAnything()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("deletion@example.test");
        using var other = await host.CreateUserAsync("foreign@example.test");
        await SeedAsync(host, "deletion@example.test", 12);
        await SeedAsync(host, "foreign@example.test", 1);
        var summary = (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!;
        var sales = (await client.GetFromJsonAsync<PageDto<SaleDto>>("/api/query/sales"))!;
        var foreign = (await other.GetFromJsonAsync<PageDto<SaleDto>>("/api/query/sales"))!.Items[0];
        var patch = new WorkspacePatchDto([], [], [], [], new([], [], [], [sales.Items[0].Id]), summary.Revision);
        var deleted = await client.PatchAsJsonAsync("/api/data", patch);
        deleted.EnsureSuccessStatusCode();
        var canonical = (await deleted.Content.ReadFromJsonAsync<DataSnapshotDto>())!;
        Assert.Empty(canonical.Sales);
        var after = (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!;
        Assert.Equal(11, after.Counts.Sales);
        Assert.Equal(12, after.Counts.Productions);
        Assert.Equal(12 * 8 + 1, after.AvailablePackages);
        var foreignMarket = (await other.GetFromJsonAsync<PageDto<MarketItemDto>>("/api/query/market"))!.Items[0];
        var foreignMarketPatch = new WorkspacePatchDto([foreignMarket with { Name = "Stolen" }], [], [], [],
            new([], [], [], []), after.Revision);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync("/api/data", foreignMarketPatch)).StatusCode);
        Assert.Equal(after.Revision, (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!.Revision);

        var foreignPatch = patch with
        {
            Sales = [foreign with { ProductionId = sales.Items[1].ProductionId }],
            Deleted = new([], [], [], []), Revision = after.Revision,
        };
        Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync("/api/data", foreignPatch)).StatusCode);
        Assert.Equal(after.Revision, (await client.GetFromJsonAsync<WorkspaceSummaryDto>("/api/query/summary"))!.Revision);
        var foreignDelete = patch with { Deleted = new([], [], [], [foreign.Id]), Revision = after.Revision };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync("/api/data", foreignDelete)).StatusCode);
    }

    [Fact]
    public async Task CompactPurchaseMutationsDoNotReturnTheWholePurchase()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("compact@example.test");
        await SeedAsync(host, "compact@example.test", 1);
        var item = (await client.GetFromJsonAsync<PageDto<MarketItemDto>>("/api/query/market"))!.Items[0];
        using var create = await client.PostAsJsonAsync("/api/stock?compact=true", new
        {
            marketItemId = item.Id, entryDate = "2026-10-28", packageCount = 30,
            packageQty = 5, packagePrice = 10, revision = 0,
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(create.Headers.Location);
        Assert.True((await create.Content.ReadAsByteArrayAsync()).Length < 1200);
        var created = (await create.Content.ReadFromJsonAsync<StockMutationResponseDto>())!;
        Assert.Null(created.Lots);
        Assert.Equal(1, created.Revision);
        var purchase = (await client.GetFromJsonAsync<PageDto<StockLotDto>>("/api/query/stock?entryOrder=1&pageSize=100"))!;
        Assert.Equal(30, purchase.TotalCount);
        using var update = await client.PutAsJsonAsync("/api/stock?compact=true", new
        {
            lotIds = purchase.Items.Select(lot => lot.Id).ToArray(),
            packageQty = 6, reason = "Correção da compra", revision = created.Revision,
        });
        update.EnsureSuccessStatusCode();
        Assert.True((await update.Content.ReadAsByteArrayAsync()).Length < 1200);
        var updated = (await update.Content.ReadFromJsonAsync<StockMutationResponseDto>())!;
        Assert.Null(updated.Lots);
        Assert.Equal(2, updated.Revision);
        Assert.Equal(6, updated.Lot.RemainingQty);
        var page = (await client.GetFromJsonAsync<PageDto<StockLotDto>>("/api/stock"))!;
        Assert.Equal(10, page.Items.Count);
        Assert.Equal(31, page.TotalCount);
    }

    internal static Task SeedAsync(ApiTestHost host, string email, int count) =>
        host.WithDatabaseAsync(async db =>
        {
            var userId = await db.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync();
            for (var index = 0; index < count; index++)
            {
                var id = $"{userId}-{index:D3}";
                db.MarketItems.Add(new MarketItem { Id = id, UserId = userId, Name = $"Item {index:D3}", Unit = "kg" });
                db.Recipes.Add(new Recipe
                {
                    Id = id, UserId = userId, Name = $"Recipe {index:D3}", Category = "Test",
                    Yield = 10, HoursPerBatch = 1,
                });
                db.Ingredients.Add(new Ingredient { Id = id, UserId = userId, RecipeId = id, MarketItemId = id, Quantity = 1 });
                db.Productions.Add(new Production
                {
                    Id = id, UserId = userId, RecipeId = id, RecipeName = $"Recipe {index:D3}",
                    Date = new(2026, 10, 1), Quantity = 1, ActualYield = 10, PackageQty = 1,
                    YieldPerBatch = 10, IngredientCostPerBatch = 20, TotalCost = 20, LostPackages = 1,
                });
                db.Sales.Add(new Sale
                {
                    Id = id, UserId = userId, ProductionId = id, RecipeName = $"Recipe {index:D3}",
                    ProductionDate = new(2026, 10, 1), Date = new(2026, 10, 2),
                    Packages = 1, PricePerPackage = 5, CostPerPackage = 2,
                    Customer = "Customer", Notes = "", Paid = true,
                });
                db.StockLots.Add(new StockLot
                {
                    Id = id, UserId = userId, MarketItemId = id, ItemName = $"Item {index:D3}", Unit = "kg",
                    EntryDate = new(2026, 9, 1), EntryOrder = index, PackageQty = 5,
                    PackagePrice = 10, RemainingQty = 5, IsActive = true,
                });
            }
            await db.SaveChangesAsync();
        });
}

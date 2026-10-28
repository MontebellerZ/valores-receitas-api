using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.DTOs;
using Xunit;

namespace ValoresReceitas.Api.Tests;

public sealed class InventoryFlowTests
{
    private static readonly DateOnly Today = new(2026, 10, 17);
    private static DataSnapshotDto Catalog => new(
        [new("flour", "Farinha", "g"), new("milk", "Leite", "ml")],
        [new("bread", "Pão", "", 10, 1, [new("ingredient", "flour", 750)])], [], []);

    private static ProductionDto Production(string id = "production", decimal quantity = 1) =>
        new(id, Today, "bread", "Nome enviado pelo cliente", quantity, 10 * quantity, 0, 50, 1, 999, 999, null, 999);

    private static async Task<DataSnapshotDto> Load(HttpClient client) =>
        (await client.GetFromJsonAsync<DataSnapshotDto>("/api/data"))!;

    private static async Task<DataSnapshotDto> Save(HttpClient client, DataSnapshotDto snapshot)
    {
        using var response = await client.PutAsJsonAsync("/api/data", snapshot);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<DataSnapshotDto>())!;
    }

    private static async Task<StockLotDto> Buy(HttpClient client, decimal price,
        DateOnly? date = null, string itemId = "flour", decimal quantity = 1000)
    {
        var snapshot = await Load(client);
        using var response = await client.PostAsJsonAsync("/api/stock", new CreateStockLotRequestDto
        {
            MarketItemId = itemId, EntryDate = date ?? Today, PackageQty = quantity,
            PackagePrice = price, Revision = snapshot.Revision!.Value,
        });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<StockMutationResponseDto>())!;
        Assert.EndsWith($"/api/stock/{result.Lot.Id}", response.Headers.Location!.ToString());
        return result.Lot;
    }

    [Fact]
    public async Task FourPackagesCreateFourIndependentLotsWithIndividualPricesAndOneRevision()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("batch@example.test");
        var date = new DateOnly(2026, 10, 19);
        await Save(client, Catalog with
        {
            Market = [new("butter", "Manteiga BH", "g")],
            Recipes = [Catalog.Recipes[0] with { Ingredients = [new("ingredient", "butter", 250)] }],
        });
        var before = await Load(client);
        var request = new CreateStockLotRequestDto
        {
            MarketItemId = "butter", EntryDate = date, PackageCount = 4,
            PackageQty = 200, PackagePrice = 7.98m, Revision = before.Revision!.Value,
        };
        using var response = await client.PostAsJsonAsync("/api/stock", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<StockMutationResponseDto>())!;
        Assert.Equal(4, result.Lots!.Count);
        Assert.Contains(result.Lot, result.Lots);
        Assert.Equal(before.Revision + 1, result.Revision);
        var lots = (await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!;
        Assert.Equal(4, lots.Select(lot => lot.Id).Distinct().Count());
        Assert.All(lots, lot =>
        {
            Assert.Equal("butter", lot.MarketItemId);
            Assert.Equal(date, lot.EntryDate);
            Assert.Equal(200m, lot.PackageQty);
            Assert.Equal(200m, lot.RemainingQty);
            Assert.Equal(7.98m, lot.PackagePrice);
        });
        Assert.Equal(800m, lots.Sum(lot => lot.RemainingQty));
        Assert.Equal(31.92m, lots.Sum(lot => lot.PackagePrice));
        using var stale = await client.PostAsJsonAsync("/api/stock", request);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var snapshot = await Load(client);
        Assert.Equal(result.Revision, snapshot.Revision);
        snapshot = await Save(client, snapshot with { Productions = [Production() with { Date = date }] });
        Assert.Equal(9.975m, Assert.Single(snapshot.Productions).TotalCost);
        var remaining = (await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!;
        Assert.Equal(3, remaining.Count);
        Assert.Equal(550m, remaining.Sum(lot => lot.RemainingQty));
        Assert.Equal(2, (await client.GetFromJsonAsync<List<StockConsumptionDto>>("/api/productions/production/consumptions"))!.Count);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(1.5)]
    [InlineData(null)]
    [InlineData(2147483648d)]
    public async Task InvalidPackageCountsDoNotCreateLotsOrAdvanceRevision(double? count)
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("invalid-count@example.test");
        await Save(client, Catalog);
        var before = await Load(client);
        using var response = await client.PostAsJsonAsync("/api/stock", new
        {
            marketItemId = "flour", entryDate = Today, packageQty = 200,
            packagePrice = 7.98m, packageCount = count, revision = before.Revision,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before.Revision, (await Load(client)).Revision);
        Assert.Empty((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!);
    }

    [Fact]
    public async Task BatchFailureRollsBackAllLotsAndRevision()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("batch-rollback@example.test");
        await Save(client, Catalog);
        var before = await Load(client);
        await host.WithDatabaseAsync(db => db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER FailBatch BEFORE INSERT ON StockLots
            WHEN (SELECT COUNT(*) FROM StockLots) >= 2
            BEGIN SELECT RAISE(ABORT, 'Simulated batch failure'); END;
            """));
        using var response = await client.PostAsJsonAsync("/api/stock", new CreateStockLotRequestDto
        {
            MarketItemId = "flour", EntryDate = Today, PackageCount = 4,
            PackageQty = 200, PackagePrice = 7.98m, Revision = before.Revision!.Value,
        });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(before.Revision, (await Load(client)).Revision);
        Assert.Empty((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!);
    }

    [Fact]
    public async Task FifoUsesPurchaseDateThenEntryOrderAndRecordsExactPartialCosts()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("fifo@example.test");
        await Save(client, Catalog with
        {
            Recipes = [Catalog.Recipes[0] with { Ingredients = [new("ingredient", "flour", 1250)] }],
        });
        var recent = await Buy(client, 20, Today);
        var oldest = await Buy(client, 10, Today.AddDays(-3));
        var sameDate = await Buy(client, 30, Today.AddDays(-3));
        var snapshot = await Load(client);
        snapshot = await Save(client, snapshot with { Productions = [Production()] });
        var production = Assert.Single(snapshot.Productions);
        Assert.Equal(17.5m, production.TotalCost);
        Assert.Equal(17.5m, production.IngredientCostPerBatch);
        Assert.Equal("Pão", production.RecipeName);
        Assert.Equal(10, production.YieldPerBatch);
        var consumptions = (await client.GetFromJsonAsync<List<StockConsumptionDto>>("/api/productions/production/consumptions"))!;
        Assert.Equal(new[] { oldest.Id, sameDate.Id }, consumptions.Select(item => item.StockLotId));
        Assert.Equal(new[] { 1000m, 250m }, consumptions.Select(item => item.Quantity));
        Assert.Equal(new[] { 10m, 7.5m }, consumptions.Select(item => item.Cost));
        var lots = (await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!;
        Assert.Equal(2, lots.Count);
        Assert.Equal(750, lots.Single(lot => lot.Id == sameDate.Id).RemainingQty);
        Assert.Equal(1000, lots.Single(lot => lot.Id == recent.Id).RemainingQty);
        await host.WithDatabaseAsync(async db =>
        {
            Assert.Equal(3, await db.StockLots.CountAsync());
            var inactive = await db.StockLots.SingleAsync(lot => lot.Id == oldest.Id);
            Assert.False(inactive.IsActive);
            Assert.Equal(0, inactive.RemainingQty);
        });
        snapshot = await Save(client, snapshot with { Productions = [production with { IngredientCostPerBatch = 1, TotalCost = 1 }] });
        Assert.Equal(17.5m, snapshot.Productions[0].TotalCost);
        await host.WithDatabaseAsync(async db => Assert.Equal(2, await db.StockConsumptions.CountAsync()));
    }

    [Fact]
    public async Task MultipleProductionsInOneSaveShareRemainingStockWithoutDoubleConsumption()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("multiple@example.test");
        await Save(client, Catalog);
        await Buy(client, 10);
        await Buy(client, 30);
        var snapshot = await Load(client);
        snapshot = await Save(client, snapshot with { Productions = [Production("one"), Production("two")] });
        Assert.Equal(7.5m, snapshot.Productions.Single(item => item.Id == "one").TotalCost);
        Assert.Equal(17.5m, snapshot.Productions.Single(item => item.Id == "two").TotalCost);
        Assert.Equal(500, Assert.Single((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!).RemainingQty);
        await Save(client, snapshot);
        await host.WithDatabaseAsync(async db => Assert.Equal(3, await db.StockConsumptions.CountAsync()));
    }

    [Fact]
    public async Task InsufficientIngredientRollsBackEveryConsumptionAndRevision()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("shortage@example.test");
        var catalog = Catalog with { Recipes = [Catalog.Recipes[0] with
        {
            Ingredients = [new("ingredient", "flour", 750), new("milk-ingredient", "milk", 100)],
        }] };
        await Save(client, catalog);
        await Buy(client, 10);
        var before = await Load(client);
        using var response = await client.PutAsJsonAsync("/api/data", before with { Productions = [Production()] });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Leite", await response.Content.ReadAsStringAsync());
        var after = await Load(client);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Empty(after.Productions);
        Assert.Equal(1000, Assert.Single((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!).RemainingQty);
        await host.WithDatabaseAsync(async db => Assert.Empty(await db.StockConsumptions.ToListAsync()));
        await Buy(client, 2, itemId: "milk", quantity: 100);
        after = await Load(client);
        await Save(client, after with { Productions = [Production()] });
        Assert.Equal(250, Assert.Single((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!).RemainingQty);
    }

    [Fact]
    public async Task ManualAdjustmentsAreAuditedAndInactiveLotsCannotBeReusedOrDeleted()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("adjust@example.test");
        await Save(client, Catalog);
        var lot = await Buy(client, 10);
        var snapshot = await Load(client);
        using var partial = await client.PatchAsJsonAsync($"/api/stock/{lot.Id}", new AdjustStockLotRequestDto
        {
            RemainingQty = 800, Reason = "Uso fora da produção", Revision = snapshot.Revision!.Value,
        });
        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        snapshot = await Load(client);
        using var increase = await client.PatchAsJsonAsync($"/api/stock/{lot.Id}", new AdjustStockLotRequestDto
        {
            RemainingQty = 900, Reason = "Inválido", Revision = snapshot.Revision!.Value,
        });
        Assert.Equal(HttpStatusCode.BadRequest, increase.StatusCode);
        using var finish = await client.PatchAsJsonAsync($"/api/stock/{lot.Id}", new AdjustStockLotRequestDto
        {
            RemainingQty = 0, Reason = "Descarte", Revision = snapshot.Revision!.Value,
        });
        Assert.Equal(HttpStatusCode.OK, finish.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!);
        snapshot = await Load(client);
        Assert.True(snapshot.Market.Single(item => item.Id == "flour").HasStockHistory);
        using var removeItem = await client.PutAsJsonAsync("/api/data", snapshot with { Market = [], Recipes = [] });
        Assert.Equal(HttpStatusCode.Conflict, removeItem.StatusCode);
        using var consume = await client.PutAsJsonAsync("/api/data", snapshot with { Productions = [Production()] });
        Assert.Equal(HttpStatusCode.Conflict, consume.StatusCode);
        using var deletion = await client.DeleteAsync($"/api/stock/{lot.Id}?revision={snapshot.Revision}");
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        await host.WithDatabaseAsync(async db =>
        {
            Assert.Equal(0, await db.StockLots.CountAsync());
            Assert.Equal(1, await db.StockLots.IgnoreQueryFilters().CountAsync());
            Assert.Equal(2, await db.StockAdjustments.CountAsync());
            Assert.False((await db.StockLots.IgnoreQueryFilters().SingleAsync()).IsActive);
        });
    }

    [Fact]
    public async Task DraftsConsumeOnlyWhenSavedAndProductionRemovalPreservesAudit()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("draft@example.test");
        await Save(client, Catalog);
        await Buy(client, 10);
        var snapshot = await Load(client);
        snapshot = await Save(client, snapshot with { Productions = [Production(quantity: 0)] });
        Assert.Null(snapshot.Productions[0].TotalCost);
        Assert.Equal(0, snapshot.Productions[0].IngredientCostPerBatch);
        Assert.Equal(1000, Assert.Single((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!).RemainingQty);
        snapshot = await Save(client, snapshot with { Productions = [Production()] });
        using var edit = await client.PutAsJsonAsync("/api/data", snapshot with { Productions = [Production(quantity: 2)] });
        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);
        snapshot = await Save(client, snapshot with { Productions = [] });
        Assert.Empty(snapshot.Productions);
        Assert.Equal(250, Assert.Single((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!).RemainingQty);
        await host.WithDatabaseAsync(async db =>
        {
            Assert.True((await db.Productions.IgnoreQueryFilters().SingleAsync()).IsDeleted);
            Assert.Single(await db.StockConsumptions.ToListAsync());
        });
        Assert.Single((await client.GetFromJsonAsync<List<StockConsumptionDto>>("/api/productions/production/consumptions"))!);
        using var reuse = await client.PutAsJsonAsync("/api/data", snapshot with { Productions = [Production()] });
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
    }

    [Fact]
    public async Task StockAndAuditAreIsolatedAndStaleWritesCannotOverspend()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var owner = await host.CreateUserAsync("stock-owner@example.test");
        using var stranger = await host.CreateUserAsync("stranger@example.test");
        await Save(owner, Catalog);
        var lot = await Buy(owner, 10);
        var stale = await Load(owner);
        await Buy(owner, 20);
        using var staleSave = await owner.PutAsJsonAsync("/api/data", stale with { Productions = [Production()] });
        Assert.Equal(HttpStatusCode.Conflict, staleSave.StatusCode);
        using var missingVersion = await owner.PutAsJsonAsync("/api/data", stale with { Revision = null });
        Assert.Equal(HttpStatusCode.Conflict, missingVersion.StatusCode);
        var current = await Load(owner);
        await Save(owner, current with { Productions = [Production()] });
        Assert.Empty((await stranger.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!);
        Assert.Empty((await stranger.GetFromJsonAsync<List<StockConsumptionDto>>("/api/productions/production/consumptions"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/stock/{lot.Id}")).StatusCode);
        var strangerState = await Load(stranger);
        using var wrongOwner = await stranger.PatchAsJsonAsync($"/api/stock/{lot.Id}", new AdjustStockLotRequestDto
        {
            RemainingQty = 0, Reason = "Tentativa", Revision = strangerState.Revision!.Value,
        });
        Assert.Equal(HttpStatusCode.BadRequest, wrongOwner.StatusCode);
        Assert.Equal(1250, (await owner.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!.Sum(item => item.RemainingQty));
    }

    [Fact]
    public async Task CatalogChangesCannotRewritePackagesOrHistoricalCosts()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("history@example.test");
        await Save(client, Catalog);
        var lot = await Buy(client, 10);
        var snapshot = await Load(client);
        snapshot = await Save(client, snapshot with { Productions = [Production()] });
        snapshot = await Save(client, snapshot with
        {
            Market = snapshot.Market.Select(item => item with { Name = item.Name + " nova" }).ToList(),
            Recipes = [snapshot.Recipes[0] with { Ingredients = [new("ingredient", "flour", 25)] }],
        });
        Assert.Equal(7.5m, snapshot.Productions[0].TotalCost);
        var stored = (await client.GetFromJsonAsync<StockLotDto>($"/api/stock/{lot.Id}"))!;
        Assert.Equal("Farinha", stored.ItemName);
        Assert.Equal(1000, stored.PackageQty);
        Assert.Equal(10, stored.PackagePrice);
        using var unitChange = await client.PutAsJsonAsync("/api/data", snapshot with
        {
            Market = snapshot.Market.Select(item => item with { Unit = "kg" }).ToList(),
        });
        Assert.Equal(HttpStatusCode.Conflict, unitChange.StatusCode);
    }

    [Fact]
    public async Task DifferentPackageSizesShareOneIngredientAndUseTheirOwnFifoCosts()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("fixed-package@example.test");
        await Save(client, Catalog with
        {
            Market = [new("butter", "Manteiga BH", "g")],
            Recipes = [Catalog.Recipes[0] with { Ingredients = [new("ingredient", "butter", 300)] }],
        });
        var small = await Buy(client, 8, itemId: "butter", quantity: 200);
        var large = await Buy(client, 15, itemId: "butter", quantity: 500);
        var snapshot = await Load(client);
        Assert.Single(snapshot.Market);
        Assert.Equal(700, (await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!.Sum(lot => lot.RemainingQty));
        snapshot = await Save(client, snapshot with { Productions = [Production()] });
        Assert.Equal(11m, Assert.Single(snapshot.Productions).TotalCost);
        var consumptions = (await client.GetFromJsonAsync<List<StockConsumptionDto>>("/api/productions/production/consumptions"))!;
        Assert.Equal(new[] { small.Id, large.Id }, consumptions.Select(item => item.StockLotId));
        Assert.Equal(new[] { 200m, 100m }, consumptions.Select(item => item.Quantity));
        Assert.Equal(new[] { 8m, 3m }, consumptions.Select(item => item.Cost));
        var remaining = Assert.Single((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!);
        Assert.Equal(large.Id, remaining.Id);
        Assert.Equal(400m, remaining.RemainingQty);
        Assert.Equal(500m, remaining.PackageQty);
    }

    [Fact]
    public async Task ShortageInSecondProductionRollsBackFirstProductionToo()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("atomic@example.test");
        await Save(client, Catalog);
        await Buy(client, 10);
        var before = await Load(client);
        using var response = await client.PutAsJsonAsync("/api/data", before with
        {
            Productions = [Production("first"), Production("second")],
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var after = await Load(client);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Empty(after.Productions);
        Assert.Equal(1000, Assert.Single((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!).RemainingQty);
        await host.WithDatabaseAsync(async db => Assert.Empty(await db.StockConsumptions.ToListAsync()));
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData(0d, 10)]
    [InlineData(-1d, 10)]
    [InlineData(0.00001, 10)]
    [InlineData(200d, -1)]
    [InlineData(200d, 1.001)]
    public async Task InvalidPurchaseQuantitiesAndPricesDoNotCreateLotsOrAdvanceRevision(double? quantity, double price)
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("invalid-purchase@example.test");
        await Save(client, Catalog);
        var before = await Load(client);
        using var response = await client.PostAsJsonAsync("/api/stock", new
        {
            marketItemId = "flour", entryDate = Today,
            packageQty = quantity, packagePrice = price, revision = before.Revision,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before.Revision, (await Load(client)).Revision);
        Assert.Empty((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!);
    }

    [Fact]
    public async Task ExistingHistoricalProductionKeepsOriginalCostWithoutInventingStock()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("legacy-production@example.test");
        await host.WithDatabaseAsync(async db =>
        {
            db.Productions.Add(new Models.Production
            {
                Id = "historical", UserId = (await db.Users.SingleAsync()).Id,
                RecipeId = "old-recipe", RecipeName = "Receita histórica", Date = Today.AddDays(-30),
                Quantity = 2, ActualYield = 20, PackageQty = 1, YieldPerBatch = 10, IngredientCostPerBatch = 7,
            });
            await db.SaveChangesAsync();
        });
        var before = await Load(client);
        var after = await Save(client, before with { Productions = [before.Productions[0] with { IngredientCostPerBatch = 100 }] });
        Assert.Null(after.Productions[0].TotalCost);
        Assert.Equal(7, after.Productions[0].IngredientCostPerBatch);
        Assert.Empty((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!);
        await host.WithDatabaseAsync(async db => Assert.Empty(await db.StockConsumptions.ToListAsync()));
    }

    [Fact]
    public async Task FractionalConsumptionKeepsAuthoritativeTotalWithoutPerBatchRoundingLoss()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("decimal@example.test");
        await Save(client, Catalog with
        {
            Recipes = [Catalog.Recipes[0] with { Ingredients = [new("ingredient", "flour", 1)] }],
        });
        await Buy(client, 1, quantity: 3);
        var snapshot = await Load(client);
        snapshot = await Save(client, snapshot with { Productions = [Production(quantity: 3)] });
        Assert.Equal(1m, snapshot.Productions[0].TotalCost);
        Assert.Equal(0.33333333m, snapshot.Productions[0].IngredientCostPerBatch);
        Assert.Empty((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!);
        Assert.Equal(1m, Assert.Single((await client.GetFromJsonAsync<List<StockConsumptionDto>>("/api/productions/production/consumptions"))!).Cost);
    }

    [Fact]
    public async Task StandaloneProductionUsesManualCostWithoutConsumingStock()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("standalone@example.test");
        await Save(client, Catalog with { Recipes = [Catalog.Recipes[0] with { Ingredients = [] }] });
        var snapshot = await Load(client);
        var standalone = Production(quantity: 3) with { TotalCost = 30.5m, IsStandalone = true };
        snapshot = await Save(client, snapshot with { Productions = [standalone] });
        var saved = Assert.Single(snapshot.Productions);
        Assert.True(saved.IsStandalone);
        Assert.Equal(30.5m, saved.TotalCost);
        Assert.Equal(10.16666667m, saved.IngredientCostPerBatch);
        Assert.Equal("Pão", saved.RecipeName);
        Assert.Equal(10, saved.YieldPerBatch);
        Assert.Empty((await client.GetFromJsonAsync<List<StockConsumptionDto>>("/api/productions/production/consumptions"))!);
        await host.WithDatabaseAsync(async db => Assert.Empty(await db.StockConsumptions.ToListAsync()));

        snapshot = await Save(client, snapshot with { Productions = [saved with { TotalCost = 12m }] });
        Assert.Equal(12m, snapshot.Productions[0].TotalCost);
        Assert.Equal(4m, snapshot.Productions[0].IngredientCostPerBatch);

        using var typeChange = await client.PutAsJsonAsync("/api/data",
            snapshot with { Productions = [snapshot.Productions[0] with { IsStandalone = false }] });
        Assert.Equal(HttpStatusCode.BadRequest, typeChange.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1.0)]
    [InlineData(1.005)]
    public async Task StandaloneProductionRequiresValidManualCost(double? cost)
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("standalone-invalid@example.test");
        var snapshot = await Save(client, Catalog);
        using var response = await client.PutAsJsonAsync("/api/data", snapshot with
        {
            Productions = [Production() with { TotalCost = (decimal?)cost, IsStandalone = true }],
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await Load(client)).Productions);
    }

    [Fact]
    public async Task LostPackagesArePersistedAndReduceAvailablePackages()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("lost-packages@example.test");
        await Save(client, Catalog with { Recipes = [Catalog.Recipes[0] with { Ingredients = [] }] });
        var snapshot = await Load(client);
        snapshot = await Save(client, snapshot with
        {
            Productions = [Production() with { TotalCost = 10m, IsStandalone = true }],
        });
        var production = Assert.Single(snapshot.Productions);
        Assert.Equal(0, production.LostPackages);

        snapshot = await Save(client, snapshot with { Productions = [production with { LostPackages = 7 }] });
        production = Assert.Single(snapshot.Productions);
        Assert.Equal(7, production.LostPackages);

        SaleDto Sale(int packages) => new("sale", production.Id, production.Date, production.RecipeName,
            production.Date, packages, 2, 1, "", true, "");
        using var oversold = await client.PutAsJsonAsync("/api/data", snapshot with { Sales = [Sale(4)] });
        Assert.Equal(HttpStatusCode.BadRequest, oversold.StatusCode);
        snapshot = await Save(client, snapshot with { Sales = [Sale(3)] });
        Assert.Equal(3, Assert.Single(snapshot.Sales).Packages);

        foreach (var lost in new[] { -1, 8, 11 })
        {
            using var invalid = await client.PutAsJsonAsync("/api/data",
                snapshot with { Productions = [production with { LostPackages = lost }] });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        Assert.Equal(7, Assert.Single((await Load(client)).Productions).LostPackages);
    }
}
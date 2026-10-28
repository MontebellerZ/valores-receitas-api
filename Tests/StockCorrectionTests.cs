using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.DTOs;
using Xunit;

namespace ValoresReceitas.Api.Tests;

public sealed class StockCorrectionTests
{
    private static readonly DateOnly Today = new(2026, 10, 17);
    private static DataSnapshotDto Catalog => new(
        [new("flour", "Farinha", "g"), new("sugar", "Açúcar", "g"), new("milk", "Leite", "ml")],
        [new("bread", "Pão", "", 10, 1, [new("ingredient", "flour", 750)])], [], []);

    private static async Task<DataSnapshotDto> Load(HttpClient client) =>
        (await client.GetFromJsonAsync<DataSnapshotDto>("/api/data"))!;

    private static async Task<DataSnapshotDto> Save(HttpClient client, DataSnapshotDto data)
    {
        using var response = await client.PutAsJsonAsync("/api/data", data);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<DataSnapshotDto>())!;
    }

    private static async Task<List<StockLotDto>> Buy(HttpClient client, int count = 1, decimal qty = 1000)
    {
        using var response = await client.PostAsJsonAsync("/api/stock", new CreateStockLotRequestDto
        {
            MarketItemId = "flour", EntryDate = Today, PackageCount = count, PackageQty = qty,
            PackagePrice = 10, Revision = (await Load(client)).Revision!.Value,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StockMutationResponseDto>())!.Lots!.ToList();
    }

    private static async Task<StockMutationResponseDto> Correct(HttpClient client, UpdateStockLotsRequestDto request)
    {
        using var response = await client.PutAsJsonAsync("/api/stock", request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<StockMutationResponseDto>())!;
    }

    private static async Task<UpdateStockLotsRequestDto> Request(HttpClient client, IEnumerable<StockLotDto> lots) =>
        new() { LotIds = lots.Select(lot => lot.Id).ToList(), Reason = "Erro de digitação",
            Revision = (await Load(client)).Revision!.Value };

    private static ProductionDto Production(string id) =>
        new(id, Today, "bread", "Pão", 1, 10, 0, 50, 1, 10, 0, null, null);

    [Fact]
    public async Task CorrectsEveryFieldForOriginalPurchaseAndAuditsEachPackage()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("correction@example.test");
        await Save(client, Catalog);
        var lots = await Buy(client, 4);
        var request = (await Request(client, lots)) with
        {
            MarketItemId = "sugar", EntryDate = Today.AddDays(-2), PackageQty = 500, PackagePrice = 7.98m,
        };
        var result = await Correct(client, request);
        Assert.Equal(request.Revision + 1, result.Revision);
        Assert.Equal(4, result.Lots!.Count);
        var saved = (await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!;
        Assert.All(saved, lot =>
        {
            Assert.Equal("sugar", lot.MarketItemId);
            Assert.Equal("Açúcar", lot.ItemName);
            Assert.Equal("g", lot.Unit);
            Assert.Equal(Today.AddDays(-2), lot.EntryDate);
            Assert.Equal(500m, lot.PackageQty);
            Assert.Equal(500m, lot.RemainingQty);
            Assert.Equal(7.98m, lot.PackagePrice);
            Assert.Equal(lots[0].EntryOrder, lot.EntryOrder);
        });
        await host.WithDatabaseAsync(async db =>
        {
            var audit = await db.StockAdjustments.ToListAsync();
            Assert.Equal(4, audit.Count);
            Assert.All(audit, record =>
            {
                var before = JsonSerializer.Deserialize<StockLotDto>(record.PreviousData!)!;
                var after = JsonSerializer.Deserialize<StockLotDto>(record.CorrectedData!)!;
                Assert.Equal(10m, before.PackagePrice);
                Assert.Equal(7.98m, after.PackagePrice);
                Assert.Equal("Erro de digitação", record.Reason);
            });
        });
        using var stale = await client.PutAsJsonAsync("/api/stock", request);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task PriceOnlyCorrectionPreservesHeterogeneousFields()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("partial@example.test");
        await Save(client, Catalog);
        var lots = await Buy(client, 2);
        lots[0] = (await Correct(client, (await Request(client, lots.Take(1))) with { PackageQty = 500 })).Lot;
        await Correct(client, (await Request(client, lots)) with { PackagePrice = 0 });
        var saved = (await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!;
        Assert.All(saved, lot =>
        {
            Assert.Equal(0m, lot.PackagePrice);
            Assert.Equal(lots.Single(old => old.Id == lot.Id).PackageQty, lot.PackageQty);
            Assert.Equal(lots.Single(old => old.Id == lot.Id).EntryOrder, lot.EntryOrder);
        });
    }

    [Fact]
    public async Task JointCorrectionsRejectDifferentPurchasesPartialPurchasesAndDifferentItems()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("purchase-boundary@example.test");
        await Save(client, Catalog);
        var purchase = await Buy(client, 3);
        var otherPurchase = await Buy(client);
        var revision = (await Load(client)).Revision;
        foreach (var lots in new[] { purchase.Take(2).ToList(), purchase.Concat(otherPurchase).ToList() })
        {
            using var response = await client.PutAsJsonAsync("/api/stock",
                (await Request(client, lots)) with { PackagePrice = 20 });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(revision, (await Load(client)).Revision);
        }
        await Correct(client, (await Request(client, purchase.Take(1))) with { MarketItemId = "sugar" });
        var before = (await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock?includeInactive=true"))!;
        revision = (await Load(client)).Revision;
        using var mixed = await client.PutAsJsonAsync("/api/stock",
            (await Request(client, purchase)) with { PackagePrice = 20 });
        Assert.Equal(HttpStatusCode.BadRequest, mixed.StatusCode);
        Assert.Equal(revision, (await Load(client)).Revision);
        Assert.Equal(before, (await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock?includeInactive=true"))!);
    }

    [Fact]
    public async Task JointCorrectionIncludesExhaustedPackagesOfOriginalPurchaseOnly()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("purchase-exhausted@example.test");
        await Save(client, Catalog);
        var purchase = await Buy(client, 3);
        var other = await Buy(client);
        using var adjustment = await client.PatchAsJsonAsync($"/api/stock/{purchase[0].Id}",
            new AdjustStockLotRequestDto { RemainingQty = 0, Reason = "Perda", Revision = (await Load(client)).Revision!.Value });
        Assert.True(adjustment.IsSuccessStatusCode);
        var result = await Correct(client, (await Request(client, purchase)) with { PackagePrice = 8 });
        Assert.Equal(3, result.Lots!.Count);
        Assert.All(result.Lots, lot => Assert.Equal(8m, lot.PackagePrice));
        Assert.False(result.Lots.Single(lot => lot.Id == purchase[0].Id).IsActive);
        Assert.Equal(10m, (await client.GetFromJsonAsync<StockLotDto>($"/api/stock/{other[0].Id}"))!.PackagePrice);
    }

    [Fact]
    public async Task DeletesOnlyRequestedUnusedPackageAndAdvancesRevisionOnce()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("delete-stock@example.test");
        await Save(client, Catalog);
        var lots = await Buy(client, 3);
        var revision = (await Load(client)).Revision!.Value;
        using var deleted = await client.DeleteAsync($"/api/stock/{lots[0].Id}?revision={revision}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(revision + 1, (await Load(client)).Revision);
        using var missing = await client.GetAsync($"/api/stock/{lots[0].Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var remaining = (await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock?includeInactive=true"))!;
        Assert.Equal(2, remaining.Count);
        Assert.DoesNotContain(remaining, lot => lot.Id == lots[0].Id);
        await Correct(client, (await Request(client, remaining)) with { PackagePrice = 8 });
    }

    [Fact]
    public async Task DeletingLastUnusedPackageRemovesInputStockHistoryFlag()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("delete-last-stock@example.test");
        await Save(client, Catalog);
        var lot = Assert.Single(await Buy(client));
        Assert.True((await Load(client)).Market.Single(item => item.Id == "flour").HasStockHistory);
        using var response = await client.DeleteAsync($"/api/stock/{lot.Id}?revision={(await Load(client)).Revision}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock?includeInactive=true"))!);
        Assert.False((await Load(client)).Market.Single(item => item.Id == "flour").HasStockHistory);
    }

    [Fact]
    public async Task DeletionIsSoftAndKeepsRowInDatabase()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("delete-soft@example.test");
        await Save(client, Catalog);
        var lot = Assert.Single(await Buy(client));
        using var response = await client.DeleteAsync($"/api/stock/{lot.Id}?revision={(await Load(client)).Revision}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await host.WithDatabaseAsync(async db =>
        {
            Assert.Equal(0, await db.StockLots.CountAsync());
            Assert.True((await db.StockLots.IgnoreQueryFilters().SingleAsync()).IsDeleted);
        });
    }

    [Theory]
    [InlineData("correction")]
    [InlineData("withdrawal")]
    public async Task DeletionIsAllowedForPackagesWithoutProductionUsage(string movement)
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync($"delete-ok-{movement}@example.test");
        await Save(client, Catalog);
        var lots = await Buy(client);
        if (movement == "correction")
            await Correct(client, (await Request(client, lots)) with { PackagePrice = 8 });
        else
        {
            using var adjusted = await client.PatchAsJsonAsync($"/api/stock/{lots[0].Id}",
                new AdjustStockLotRequestDto { RemainingQty = 0, Reason = "Perda", Revision = (await Load(client)).Revision!.Value });
            Assert.True(adjusted.IsSuccessStatusCode);
        }
        using var response = await client.DeleteAsync($"/api/stock/{lots[0].Id}?revision={(await Load(client)).Revision}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock?includeInactive=true"))!);
    }

    [Theory]
    [InlineData("production")]
    [InlineData("reversed")]
    public async Task DeletionPreservesPackagesUsedInProduction(string movement)
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync($"delete-{movement}@example.test");
        await Save(client, Catalog);
        var lots = await Buy(client);
        await Save(client, (await Load(client)) with { Productions = [Production("used")] });
        if (movement == "reversed")
            await Save(client, (await Load(client)) with { Productions = [] });
        var revision = (await Load(client)).Revision;
        var before = (await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock?includeInactive=true"))!;
        using var response = await client.DeleteAsync($"/api/stock/{lots[0].Id}?revision={revision}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(revision, (await Load(client)).Revision);
        Assert.Equal(before, (await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock?includeInactive=true"))!);
    }

    [Fact]
    public async Task DeletionRequiresAuthenticationOwnershipAndCurrentRevision()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var owner = await host.CreateUserAsync("delete-owner@example.test");
        using var other = await host.CreateUserAsync("delete-other@example.test");
        using var anonymous = host.CreateClient();
        await Save(owner, Catalog);
        var lot = Assert.Single(await Buy(owner));
        var revision = (await Load(owner)).Revision!.Value;
        using var unauthorized = await anonymous.DeleteAsync($"/api/stock/{lot.Id}?revision={revision}");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        var otherRevision = (await Load(other)).Revision;
        using var foreign = await other.DeleteAsync($"/api/stock/{lot.Id}?revision={otherRevision}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(otherRevision, (await Load(other)).Revision);
        using var stale = await owner.DeleteAsync($"/api/stock/{lot.Id}?revision={revision - 1}");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var missing = await owner.DeleteAsync($"/api/stock/missing?revision={revision}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(revision, (await Load(owner)).Revision);
        Assert.Single((await owner.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!);
    }

    [Fact]
    public async Task DeleteWriteFailureRollsBackPackageAndRevision()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("delete-rollback@example.test");
        await Save(client, Catalog);
        var lot = Assert.Single(await Buy(client));
        var revision = (await Load(client)).Revision;
        await host.WithDatabaseAsync(db => db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER FailDeletion BEFORE UPDATE ON StockLots
            BEGIN SELECT RAISE(ABORT, 'Simulated deletion failure'); END;
            """));
        using var response = await client.DeleteAsync($"/api/stock/{lot.Id}?revision={revision}");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(revision, (await Load(client)).Revision);
        Assert.Equal(lot, Assert.Single((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!));
    }

    [Fact]
    public async Task CorrectionsPreserveWithdrawalsAndHistoricalCostsWhileFutureConsumptionUsesNewPrice()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("history@example.test");
        await Save(client, Catalog);
        var lots = await Buy(client);
        var saved = await Save(client, (await Load(client)) with { Productions = [Production("first")] });
        Assert.Equal(7.5m, saved.Productions[0].TotalCost);
        var original = (await client.GetFromJsonAsync<List<StockConsumptionDto>>("/api/productions/first/consumptions"))!;
        var result = await Correct(client, (await Request(client, lots)) with
        {
            PackageQty = 2000, PackagePrice = 40, EntryDate = Today.AddDays(-1),
        });
        Assert.Equal(1250m, result.Lot.RemainingQty);
        Assert.Equal(original, (await client.GetFromJsonAsync<List<StockConsumptionDto>>("/api/productions/first/consumptions"))!);
        saved = await Save(client, (await Load(client)) with { Productions = [Production("first"), Production("second")] });
        Assert.Equal(7.5m, saved.Productions.Single(item => item.Id == "first").TotalCost);
        Assert.Equal(15m, saved.Productions.Single(item => item.Id == "second").TotalCost);
        Assert.Equal(500m, (await client.GetFromJsonAsync<StockLotDto>($"/api/stock/{lots[0].Id}"))!.RemainingQty);
        var second = Assert.Single((await client.GetFromJsonAsync<List<StockConsumptionDto>>("/api/productions/second/consumptions"))!);
        Assert.Equal(2000m, second.PackageQty);
        Assert.Equal(40m, second.PackagePrice);
    }

    [Fact]
    public async Task InactivePackagesCanBeCorrectedAndReactivatedWithoutErasingManualWithdrawals()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("inactive@example.test");
        await Save(client, Catalog);
        var lots = await Buy(client);
        using var adjustment = await client.PatchAsJsonAsync($"/api/stock/{lots[0].Id}",
            new AdjustStockLotRequestDto { RemainingQty = 0, Reason = "Perda", Revision = (await Load(client)).Revision!.Value });
        Assert.True(adjustment.IsSuccessStatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!);
        var inactive = Assert.Single((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock?includeInactive=true"))!);
        Assert.False(inactive.IsActive);
        var corrected = await Correct(client, (await Request(client, lots)) with { PackageQty = 1500, PackagePrice = 12 });
        Assert.Equal(500m, corrected.Lot.RemainingQty);
        Assert.True(corrected.Lot.IsActive);
        Assert.Single((await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!);
    }

    [Fact]
    public async Task ConsumedPackagesRejectSmallerQuantitiesItemChangesAndDatesAfterConsumptionAtomically()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("reject@example.test");
        await Save(client, Catalog);
        var lots = await Buy(client, 2);
        await Save(client, (await Load(client)) with { Productions = [Production("first")] });
        var before = (await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock?includeInactive=true"))!;
        var request = await Request(client, lots);
        foreach (var invalid in new[]
        {
            request with { PackageQty = 749, PackagePrice = 8 },
            request with { MarketItemId = "sugar", PackagePrice = 8 },
            request with { EntryDate = Today.AddDays(1), PackagePrice = 8 },
        })
        {
            using var rejected = await client.PutAsJsonAsync("/api/stock", invalid);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            Assert.Equal(request.Revision, (await Load(client)).Revision);
            Assert.Equal(before, (await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock?includeInactive=true"))!);
        }
        await host.WithDatabaseAsync(async db => Assert.Equal(0, await db.StockAdjustments.CountAsync()));
    }

    [Fact]
    public async Task InvalidOrForeignIdsNeverPartiallyCorrectAnOwnedPackage()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var owner = await host.CreateUserAsync("owner-edit@example.test");
        using var other = await host.CreateUserAsync("other-edit@example.test");
        using var anonymous = host.CreateClient();
        await Save(owner, Catalog);
        await Save(other, Catalog with { Market = [new("other-flour", "Farinha", "g")], Recipes = [] });
        var lots = await Buy(owner);
        var request = (await Request(owner, lots)) with { PackagePrice = 99 };
        using var unauthorized = await anonymous.PutAsJsonAsync("/api/stock", request);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        using var foreign = await other.PutAsJsonAsync("/api/stock", request with { Revision = (await Load(other)).Revision!.Value });
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        foreach (var invalid in new[]
        {
            request with { LotIds = [lots[0].Id, "missing"] },
            request with { LotIds = [lots[0].Id, lots[0].Id] },
            request with { MarketItemId = "other-flour" },
        })
        {
            using var rejected = await owner.PutAsJsonAsync("/api/stock", invalid);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            Assert.Equal(request.Revision, (await Load(owner)).Revision);
        }
        Assert.Equal(10m, (await owner.GetFromJsonAsync<StockLotDto>($"/api/stock/{lots[0].Id}"))!.PackagePrice);
    }

    [Theory]
    [InlineData("""{"lotIds":[],"packagePrice":1,"reason":"erro"}""")]
    [InlineData("""{"lotIds":null,"packagePrice":1,"reason":"erro"}""")]
    [InlineData("""{"lotIds":["id"],"reason":"erro"}""")]
    [InlineData("""{"lotIds":["id"],"packagePrice":-1,"reason":"erro"}""")]
    [InlineData("""{"lotIds":["id"],"packagePrice":1.001,"reason":"erro"}""")]
    [InlineData("""{"lotIds":["id"],"packageQty":0,"reason":"erro"}""")]
    [InlineData("""{"lotIds":["id"],"packageQty":1.00001,"reason":"erro"}""")]
    [InlineData("""{"lotIds":["id"],"entryDate":"0001-01-01","reason":"erro"}""")]
    [InlineData("""{"lotIds":["id"],"packagePrice":1,"reason":" "}""")]
    public async Task InvalidCorrectionIsRejected(string json)
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("invalid-edit@example.test");
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PutAsync("/api/stock", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuditWriteFailureRollsBackCorrectionsAndRevision()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("rollback-edit@example.test");
        await Save(client, Catalog);
        var lots = await Buy(client, 4);
        var request = (await Request(client, lots)) with { PackagePrice = 7.98m };
        await host.WithDatabaseAsync(db => db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER FailCorrection BEFORE INSERT ON StockAdjustments
            WHEN (SELECT COUNT(*) FROM StockAdjustments) >= 2
            BEGIN SELECT RAISE(ABORT, 'Simulated correction failure'); END;
            """));
        using var response = await client.PutAsJsonAsync("/api/stock", request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(request.Revision, (await Load(client)).Revision);
        Assert.Equal(lots.OrderBy(lot => lot.Id), (await client.GetFromJsonAsync<List<StockLotDto>>("/api/stock"))!.OrderBy(lot => lot.Id));
        await host.WithDatabaseAsync(async db => Assert.Equal(0, await db.StockAdjustments.CountAsync()));
    }

    [Fact]
    public async Task MigrationBackfillsLegacyConsumptionBeforeAnyCorrection()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("backfill-edit@example.test");
        await Save(client, Catalog);
        var lots = await Buy(client);
        await Save(client, (await Load(client)) with { Productions = [Production("legacy")] });
        await host.WithDatabaseAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE StockConsumptions SET EntryDate = NULL, PackageQty = NULL, PackagePrice = NULL");
            var sql = Assert.Single(new Migrations.AddStockCorrections().UpOperations
                .OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>()).Sql;
            await db.Database.ExecuteSqlRawAsync(sql);
            var consumption = await db.StockConsumptions.SingleAsync();
            Assert.Equal(Today, consumption.EntryDate);
            Assert.Equal(1000m, consumption.PackageQty);
            Assert.Equal(10m, consumption.PackagePrice);
        });
        await Correct(client, (await Request(client, lots)) with { PackagePrice = 40, PackageQty = 2000 });
        var historical = Assert.Single((await client.GetFromJsonAsync<List<StockConsumptionDto>>("/api/productions/legacy/consumptions"))!);
        Assert.Equal(10m, historical.PackagePrice);
        Assert.Equal(1000m, historical.PackageQty);
        Assert.Equal(7.5m, historical.Cost);
    }
}

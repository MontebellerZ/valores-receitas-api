using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Controllers;
using ValoresReceitas.Api.Data;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;
using Xunit;

namespace ValoresReceitas.Api.Tests;

public sealed class ApiFlowTests
{
    [Fact]
    public async Task SnapshotRoundTripAndModuleQueriesUsePublicDtos()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("owner@example.test");
        var snapshot = Snapshot("owner");
        using var save = await PutSnapshotAsync(client, snapshot);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var actual = await client.GetFromJsonAsync<DataSnapshotDto>("/api/data");
        AssertSnapshot(snapshot, actual!);
        using var raw = JsonDocument.Parse(await client.GetStringAsync("/api/data"));
        Assert.False(raw.RootElement.GetProperty("market")[0].TryGetProperty("userId", out _));
        Assert.False(raw.RootElement.GetProperty("market")[0].TryGetProperty("packageQty", out _));
        Assert.False(raw.RootElement.GetProperty("market")[0].TryGetProperty("packagePrice", out _));
        var ingredient = raw.RootElement.GetProperty("recipes")[0].GetProperty("ingredients")[0];
        Assert.False(ingredient.TryGetProperty("recipeId", out _));
        Assert.False(ingredient.TryGetProperty("userId", out _));
        Assert.Equal(snapshot.Market.Select(item => item with { IngredientCount = 1 }),
            await client.GetPageItemsAsync<MarketItemDto>("/api/market"));
        Assert.Single((await client.GetPageItemsAsync<RecipeDto>("/api/recipes"))!);
        Assert.Equal(snapshot.Recipes[0].Ingredients,
            await client.GetPageItemsAsync<IngredientDto>("/api/recipes/owner-recipe/ingredients"));
        Assert.Equal(snapshot.Productions, await client.GetPageItemsAsync<ProductionDto>("/api/productions"));
        Assert.Equal(snapshot.Sales, await client.GetPageItemsAsync<SaleDto>("/api/sales"));
        var donated = snapshot.Sales[0] with { PricePerPackage = 0, Paid = true, Donated = true };
        (await PutSnapshotAsync(client, snapshot with { Sales = [donated] })).EnsureSuccessStatusCode();
        Assert.True((await client.GetPageItemsAsync<SaleDto>("/api/sales"))!.Single().Donated);
        Assert.Equal("connected", (await client.GetFromJsonAsync<HealthResponseDto>("/api/health"))!.Database);
        using var replace = await PutSnapshotAsync(client, snapshot with { Sales = [] });
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<DataSnapshotDto>("/api/data"))!.Sales);
    }

    [Fact]
    public async Task ReadsAndReplacementsAreIsolatedByUser()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var first = await host.CreateUserAsync("first@example.test");
        using var second = await host.CreateUserAsync("second@example.test");
        (await PutSnapshotAsync(first, Snapshot("first"))).EnsureSuccessStatusCode();
        (await PutSnapshotAsync(second, Snapshot("second"))).EnsureSuccessStatusCode();
        Assert.Equal("first-market", (await first.GetFromJsonAsync<DataSnapshotDto>("/api/data"))!.Market.Single().Id);
        Assert.Empty((await second.GetPageItemsAsync<IngredientDto>("/api/recipes/first-recipe/ingredients"))!);
        (await PutSnapshotAsync(first, Snapshot("first") with { Recipes = [], Productions = [], Sales = [] })).EnsureSuccessStatusCode();
        Assert.Equal("second-market", (await second.GetFromJsonAsync<DataSnapshotDto>("/api/data"))!.Market.Single().Id);
    }

    [Theory]
    [InlineData("market")]
    [InlineData("recipe")]
    [InlineData("ingredient")]
    [InlineData("production")]
    [InlineData("sale")]
    public async Task ForeignIdentifiersReturnConflictWithoutDeletingOwnedData(string entity)
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var first = await host.CreateUserAsync("first@example.test");
        using var second = await host.CreateUserAsync("second@example.test");
        (await PutSnapshotAsync(first, Snapshot("first"))).EnsureSuccessStatusCode();
        (await PutSnapshotAsync(second, Snapshot("second"))).EnsureSuccessStatusCode();
        var payload = Snapshot("second");
        payload = entity switch
        {
            "market" => payload with
            {
                Market = [payload.Market[0] with { Id = "first-market" }],
                Recipes = [payload.Recipes[0] with { Ingredients = [payload.Recipes[0].Ingredients[0] with { MarketItemId = "first-market" }] }],
            },
            "recipe" => payload with { Recipes = [payload.Recipes[0] with { Id = "first-recipe" }] },
            "ingredient" => payload with { Recipes = [payload.Recipes[0] with { Ingredients = [payload.Recipes[0].Ingredients[0] with { Id = "first-ingredient" }] }] },
            "production" => payload with
            {
                Productions = [payload.Productions[0] with { Id = "first-production" }],
                Sales = [payload.Sales[0] with { ProductionId = "first-production" }],
            },
            _ => payload with { Sales = [payload.Sales[0] with { Id = "first-sale" }] },
        };
        using var response = await PutSnapshotAsync(second, payload);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("second-market", (await second.GetFromJsonAsync<DataSnapshotDto>("/api/data"))!.Market.Single().Id);
        Assert.Equal("first-sale", (await first.GetFromJsonAsync<DataSnapshotDto>("/api/data"))!.Sales.Single().Id);
    }

    [Theory]
    [InlineData("duplicate-id")]
    [InlineData("missing-market")]
    [InlineData("missing-production")]
    [InlineData("early-sale")]
    [InlineData("oversold")]
    [InlineData("zero-package")]
    [InlineData("missing-unit")]
    [InlineData("missing-date")]
    [InlineData("duplicate-ingredient")]
    [InlineData("null-list")]
    [InlineData("null-ingredients")]
    [InlineData("priced-donation")]
    [InlineData("null-entry")]
    public async Task InvalidSnapshotsReturnBadRequestAndPreserveData(string invalidCase)
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("owner@example.test");
        var original = Snapshot("owner");
        (await PutSnapshotAsync(client, original)).EnsureSuccessStatusCode();
        var payload = invalidCase switch
        {
            "duplicate-id" => original with { Market = [original.Market[0], original.Market[0]] },
            "missing-market" => original with { Market = [] },
            "missing-production" => original with { Productions = [] },
            "early-sale" => original with { Sales = [original.Sales[0] with { Date = new DateOnly(2026, 1, 1) }] },
            "oversold" => original with { Sales = [original.Sales[0] with { Packages = 11 }] },
            "zero-package" => original with { Productions = [original.Productions[0] with { PackageQty = 0 }] },
            "missing-unit" => original with { Market = [original.Market[0] with { Unit = "" }] },
            "missing-date" => original with { Sales = [original.Sales[0] with { Date = default }] },
            "priced-donation" => original with { Sales = [original.Sales[0] with { Donated = true, Paid = true }] },
            "duplicate-ingredient" => original with { Recipes = [original.Recipes[0] with
            {
                Ingredients = [original.Recipes[0].Ingredients[0], original.Recipes[0].Ingredients[0] with { Id = "different" }],
            }] },
            "null-list" => original with { Market = null! },
            "null-ingredients" => original with { Recipes = [original.Recipes[0] with { Ingredients = null! }] },
            _ => original with { Market = [null!] },
        };
        using var response = await PutSnapshotAsync(client, payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(400, problem!.Status);
        var actual = await client.GetFromJsonAsync<DataSnapshotDto>("/api/data");
        AssertSnapshot(original, actual!);
    }

    [Fact]
    public async Task FailedInsertRollsBackAllDeletions()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("owner@example.test");
        var original = Snapshot("owner");
        (await PutSnapshotAsync(client, original)).EnsureSuccessStatusCode();
        await host.WithDatabaseAsync(context => context.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER FailSale BEFORE INSERT ON Sales WHEN NEW.Id = 'fail' BEGIN SELECT RAISE(ABORT, 'test failure'); END;"));
        using var response = await PutSnapshotAsync(client, original with { Sales = [original.Sales[0] with { Id = "fail" }] });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var actual = await client.GetFromJsonAsync<DataSnapshotDto>("/api/data");
        AssertSnapshot(original, actual!);
    }

    [Theory]
    [InlineData("/api/data")]
    [InlineData("/api/market")]
    [InlineData("/api/recipes")]
    [InlineData("/api/recipes/test/ingredients")]
    [InlineData("/api/productions")]
    [InlineData("/api/sales")]
    [InlineData("/api/stock")]
    [InlineData("/api/productions/test/consumptions")]
    [InlineData("/api/auth/session")]
    public async Task PrivateRoutesRequireAuthentication(string route)
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = host.CreateClient();
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RegistrationConfirmationSessionAndLogoutPreserveContracts()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = host.CreateClient();
        using var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            Email = "signup@example.test", Password = ApiTestHost.Password,
        });
        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);
        var query = QueryHelpers.ParseQuery(new Uri(host.EmailSender.ConfirmationLink!).Query);
        using var confirm = await client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequestDto
        {
            UserId = query["confirmUserId"]!, Token = query["confirmToken"]!,
        });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
        await ApiTestHost.LoginAsync(client, "signup@example.test");
        Assert.Equal("signup@example.test", (await client.GetFromJsonAsync<SessionResponseDto>("/api/auth/session"))!.Email);
        using var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), value => value.Contains("expires=Thu, 01 Jan 1970"));
        client.DefaultRequestHeaders.Remove("Cookie");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task IdentityFailuresRetainStatusCodeAndValidationDetails()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("unconfirmed@example.test", confirmed: false);
        using var unconfirmed = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = "unconfirmed@example.test", Password = ApiTestHost.Password,
        });
        Assert.Equal(HttpStatusCode.Forbidden, unconfirmed.StatusCode);
        using var problem = JsonDocument.Parse(await unconfirmed.Content.ReadAsStringAsync());
        Assert.Equal("email_not_confirmed", problem.RootElement.GetProperty("code").GetString());
        using var invalid = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = "missing@example.test", Password = ApiTestHost.Password,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        using var duplicate = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            Email = "unconfirmed@example.test", Password = ApiTestHost.Password,
        });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        using var errors = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        Assert.True(errors.RootElement.GetProperty("errors").EnumerateObject().Any());
        using var resend = await client.PostAsJsonAsync("/api/auth/resend-confirmation", new EmailRequestDto { Email = "missing@example.test" });
        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
    }

    [Fact]
    public async Task EmailFailureDoesNotDeleteTheRegisteredAccount()
    {
        await using var host = await ApiTestHost.CreateAsync();
        host.EmailSender.Fail = true;
        using var client = host.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            Email = "smtp@example.test", Password = ApiTestHost.Password,
        });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        host.EmailSender.Fail = false;
        (await client.PostAsJsonAsync("/api/auth/resend-confirmation", new EmailRequestDto { Email = "smtp@example.test" })).EnsureSuccessStatusCode();
        Assert.NotNull(host.EmailSender.ConfirmationLink);
    }

    [Fact]
    public async Task LegacyDataIsClaimedOnlyByTheFirstConfirmedLogin()
    {
        await using var host = await ApiTestHost.CreateAsync();
        await host.WithDatabaseAsync(async context =>
        {
            context.MarketItems.Add(new MarketItem { Id = "legacy", Name = "Legacy", Unit = "g", PackageQty = 100 });
            await context.SaveChangesAsync();
        });
        using var first = await host.CreateUserAsync("first@example.test");
        using var second = await host.CreateUserAsync("second@example.test");
        Assert.Single((await first.GetPageItemsAsync<MarketItemDto>("/api/market"))!);
        Assert.Empty((await second.GetPageItemsAsync<MarketItemDto>("/api/market"))!);
    }

    [Fact]
    public void EntityModelStillMatchesExistingSqlServerMigrations()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelComparisonOnly;Integrated Security=true;TrustServerCertificate=true")
            .Options);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task RepeatedInvalidPasswordsLockTheAccount()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = await host.CreateUserAsync("locked@example.test");
        client.DefaultRequestHeaders.Remove("Cookie");
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
            {
                Email = "locked@example.test", Password = "Wrong-password.123",
            });
            Assert.Equal(attempt == 5 ? (HttpStatusCode)423 : HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var locked = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = "locked@example.test", Password = ApiTestHost.Password,
        });
        Assert.Equal((HttpStatusCode)423, locked.StatusCode);
    }

    [Fact]
    public async Task LoginRateLimitReturnsProblemDetailsAndRetryAfter()
    {
        await using var host = await ApiTestHost.CreateAsync(loginRateLimit: 1);
        using var client = host.CreateClient();
        using var first = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = "missing@example.test", Password = ApiTestHost.Password,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);

        using var limited = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = "missing@example.test", Password = ApiTestHost.Password,
        });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
        var problem = await limited.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(429, problem!.Status);
    }

    [Fact]
    public async Task RegistrationRequiresAtLeastTwelvePasswordCharacters()
    {
        await using var host = await ApiTestHost.CreateAsync();
        using var client = host.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            Email = "short-password@example.test",
            Password = "Short7!",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem!.Errors.Keys, key => key.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ControllersDependOnlyOnTheirServicesAndDtosDoNotExposeEntities()
    {
        var assembly = typeof(DataController).Assembly;
        var controllers = assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(ControllerBase))).ToArray();
        Assert.NotEmpty(controllers);
        Assert.All(controllers, controller => Assert.All(controller.GetConstructors().SelectMany(item => item.GetParameters()),
            parameter =>
            {
                Assert.True(parameter.ParameterType.IsInterface);
                Assert.Equal("ValoresReceitas.Api.Services.Interfaces", parameter.ParameterType.Namespace);
            }));
        var dtos = assembly.GetTypes().Where(type => type.Namespace == "ValoresReceitas.Api.DTOs");
        Assert.All(dtos.SelectMany(type => type.GetProperties()), property =>
            Assert.DoesNotContain("ValoresReceitas.Api.Models", property.PropertyType.ToString()));
        var contextConsumers = assembly.GetTypes().Where(type => type.GetConstructors()
            .SelectMany(item => item.GetParameters()).Any(parameter => parameter.ParameterType == typeof(AppDbContext)));
        Assert.All(contextConsumers, type => Assert.Equal("ValoresReceitas.Api.Repositories", type.Namespace));
    }

    private static void AssertSnapshot(DataSnapshotDto expected, DataSnapshotDto actual)
    {
        Assert.Equal(expected.Market, actual.Market);
        Assert.Equal(expected.Productions, actual.Productions);
        Assert.Equal(expected.Sales, actual.Sales);
        Assert.Equal(expected.Recipes.Count, actual.Recipes.Count);
        foreach (var (expectedRecipe, actualRecipe) in expected.Recipes.Zip(actual.Recipes))
        {
            Assert.Equal((expectedRecipe.Id, expectedRecipe.Name, expectedRecipe.Category, expectedRecipe.Yield, expectedRecipe.HoursPerBatch, expectedRecipe.ShelfLifeDays),
                (actualRecipe.Id, actualRecipe.Name, actualRecipe.Category, actualRecipe.Yield, actualRecipe.HoursPerBatch, actualRecipe.ShelfLifeDays));
            Assert.Equal(expectedRecipe.Ingredients, actualRecipe.Ingredients);
        }
    }

    private static DataSnapshotDto Snapshot(string prefix)
    {
        var date = new DateOnly(2026, 10, 16);
        return new DataSnapshotDto(
            [new MarketItemDto($"{prefix}-market", "Farinha", "g", true)],
            [new RecipeDto($"{prefix}-recipe", "Pao", "", 10, 1,
                [new IngredientDto($"{prefix}-ingredient", $"{prefix}-market", 500)], 15)],
            [new ProductionDto($"{prefix}-production", date, $"{prefix}-recipe", "Pao", 1, 10, 0, 20, 1, 10, 4, null, 4)],
            [new SaleDto($"{prefix}-sale", $"{prefix}-production", date, "Pao", date, 2, 5, 0.4m, "", false, "")]);
    }

    private static async Task<HttpResponseMessage> PutSnapshotAsync(HttpClient client, DataSnapshotDto snapshot)
    {
        var current = (await client.GetFromJsonAsync<DataSnapshotDto>("/api/data"))!;
        if (current.Market.Count == 0 && snapshot.Market.Count > 0 && snapshot.Productions.Count > 0)
        {
            using var catalog = await client.PutAsJsonAsync("/api/data", snapshot with { Productions = [], Sales = [], Revision = current.Revision });
            catalog.EnsureSuccessStatusCode();
            current = (await client.GetFromJsonAsync<DataSnapshotDto>("/api/data"))!;
            using var entry = await client.PostAsJsonAsync("/api/stock", new CreateStockLotRequestDto
            {
                MarketItemId = snapshot.Market[0].Id, EntryDate = new DateOnly(2026, 10, 1),
                PackageQty = 1000, PackagePrice = 8, Revision = current.Revision!.Value,
            });
            entry.EnsureSuccessStatusCode();
            current = (await client.GetFromJsonAsync<DataSnapshotDto>("/api/data"))!;
        }
        return await client.PutAsJsonAsync("/api/data", snapshot with { Revision = current.Revision });
    }
}
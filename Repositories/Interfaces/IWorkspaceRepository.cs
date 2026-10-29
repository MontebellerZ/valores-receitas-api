using ValoresReceitas.Api.Models;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Repositories.Interfaces;

public interface IWorkspaceRepository
{
    Task<long> RevisionAsync(string userId, CancellationToken ct);
    Task AdvanceRevisionAsync(string userId, long? expected, CancellationToken ct);
    Task<List<Production>> ProductionsAsync(string userId, CancellationToken ct);
    Task<List<Production>> ProductionsByIdsAsync(string userId, string[] ids, CancellationToken ct);
    Task<List<Sale>> SalesByIdsAsync(string userId, string[] ids, CancellationToken ct);
    Task<List<Sale>> SalesByProductionIdsAsync(string userId, string[] productionIds, CancellationToken ct);
    Task<Dictionary<string, decimal>> SalePackageTotalsAsync(string userId, string[] productionIds, string[] excludedSaleIds, CancellationToken ct);
    Task PatchMarketAsync(string userId, IReadOnlyCollection<MarketItem> changed, CancellationToken ct);
    Task PatchRecipesAsync(string userId, IReadOnlyCollection<Recipe> recipes, IReadOnlyCollection<Ingredient> ingredients, CancellationToken ct);
    Task SyncCatalogAsync(string userId, IEnumerable<MarketItem> market, IEnumerable<Recipe> recipes, IEnumerable<Ingredient> ingredients, IEnumerable<Sale> sales, CancellationToken ct);
    void AddProduction(Production production);
    void AddSale(Sale sale);
}

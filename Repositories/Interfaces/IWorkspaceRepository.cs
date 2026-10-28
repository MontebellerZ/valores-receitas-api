using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Repositories.Interfaces;

public interface IWorkspaceRepository
{
    Task<long> RevisionAsync(string userId, CancellationToken ct);
    Task AdvanceRevisionAsync(string userId, long? expected, CancellationToken ct);
    Task<List<Production>> ProductionsAsync(string userId, CancellationToken ct);
    Task SyncCatalogAsync(string userId, IEnumerable<MarketItem> market, IEnumerable<Recipe> recipes, IEnumerable<Ingredient> ingredients, IEnumerable<Sale> sales, CancellationToken ct);
    void AddProduction(Production production);
}

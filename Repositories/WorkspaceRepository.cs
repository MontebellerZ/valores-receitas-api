using ValoresReceitas.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Data;
using ValoresReceitas.Api.Models;
using ValoresReceitas.Api.Services;

namespace ValoresReceitas.Api.Repositories;

public sealed class WorkspaceRepository(AppDbContext context) : IWorkspaceRepository
{
    public Task<long> RevisionAsync(string userId, CancellationToken ct) =>
        context.Users.Where(user => user.Id == userId).Select(user => user.WorkspaceRevision).SingleAsync(ct);

    public async Task AdvanceRevisionAsync(string userId, long? expected, CancellationToken ct)
    {
        if (expected is null && await context.StockLots.AnyAsync(lot => lot.UserId == userId, ct))
            throw new ServiceException(ServiceError.Conflict, "Recarregue os dados antes de salvar o estoque.", code: "workspace_conflict");
        var revision = expected ?? await RevisionAsync(userId, ct);
        var updated = await context.Users.Where(user => user.Id == userId && user.WorkspaceRevision == revision)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.WorkspaceRevision, user => user.WorkspaceRevision + 1), ct);
        if (updated != 1)
            throw new ServiceException(ServiceError.Conflict, "Os dados foram alterados em outra sessão. Recarregue a página.", code: "workspace_conflict");
    }

    public Task<List<Production>> ProductionsAsync(string userId, CancellationToken ct) =>
        context.Productions.IgnoreQueryFilters().Where(item => item.UserId == userId).ToListAsync(ct);

    public async Task SyncCatalogAsync(string userId, IEnumerable<MarketItem> market,
        IEnumerable<Recipe> recipes, IEnumerable<Ingredient> ingredients, IEnumerable<Sale> sales, CancellationToken ct)
    {
        await SyncAsync(context.Sales.IgnoreQueryFilters().Where(item => item.UserId == userId), sales, item => item.Id, ct);
        await SyncAsync(context.Ingredients.IgnoreQueryFilters().Where(item => item.UserId == userId), ingredients, item => item.Id, ct);
        await SyncAsync(context.Recipes.IgnoreQueryFilters().Where(item => item.UserId == userId), recipes, item => item.Id, ct);
        var items = market.ToList();
        var ids = items.Select(item => item.Id).ToArray();
        if (await context.StockLots.AnyAsync(lot => lot.UserId == userId && !ids.Contains(lot.MarketItemId), ct))
            throw new ServiceException(ServiceError.Conflict, "Um insumo com histórico de estoque não pode ser excluído.");
        var existing = await context.MarketItems.Where(item => item.UserId == userId).ToListAsync(ct);
        foreach (var item in items)
        {
            var previous = existing.Find(entry => entry.Id == item.Id);
            if (previous is not null)
            {
                // Preserve legacy catalog values; new purchases use the stock lot's quantity and price.
                item.PackageQty = previous.PackageQty;
                item.PackagePrice = previous.PackagePrice;
            }
            if (previous is not null && previous.Unit != item.Unit &&
                await context.StockLots.AnyAsync(lot => lot.UserId == userId && lot.MarketItemId == item.Id, ct))
                throw new ServiceException(ServiceError.Conflict, "A unidade de um insumo com estoque não pode ser alterada. Cadastre outro insumo.");
        }
        await SyncAsync(context.MarketItems.IgnoreQueryFilters().Where(item => item.UserId == userId), items, item => item.Id, ct);
    }

    private async Task SyncAsync<T>(IQueryable<T> query, IEnumerable<T> incoming, Func<T, string> id, CancellationToken ct)
        where T : class, ISoftDeletable
    {
        var existing = (await query.ToListAsync(ct)).ToDictionary(id, StringComparer.Ordinal);
        foreach (var item in incoming)
        {
            if (existing.Remove(id(item), out var previous))
                context.Entry(previous).CurrentValues.SetValues(item);
            else
                context.Add(item);
        }
        foreach (var removed in existing.Values) removed.IsDeleted = true;
    }

    public void AddProduction(Production production) => context.Productions.Add(production);
}
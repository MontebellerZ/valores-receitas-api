using ValoresReceitas.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Data;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Repositories;

public sealed class MarketItemRepository(AppDbContext context) : IMarketItemRepository
{
    public Task<List<MarketItemDto>> ListAsync(string userId, CancellationToken cancellationToken) =>
        context.MarketItems.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.Name)
            .Select(item => new MarketItemDto(item.Id, item.Name, item.Unit,
                context.StockLots.Any(lot => lot.MarketItemId == item.Id && lot.UserId == userId)))
            .ToListAsync(cancellationToken);

    public Task<bool> HasForeignIdsAsync(string userId, string[] ids, CancellationToken cancellationToken) =>
        context.MarketItems.IgnoreQueryFilters().AnyAsync(item => item.UserId != userId && ids.Contains(item.Id), cancellationToken);

    public Task DeleteOwnedAsync(string userId, CancellationToken cancellationToken) =>
        context.MarketItems.Where(item => item.UserId == userId).ExecuteUpdateAsync(update => update.SetProperty(entity => entity.IsDeleted, true), cancellationToken);

    public void AddRange(IEnumerable<MarketItem> items) => context.MarketItems.AddRange(items);

    public Task ClaimLegacyAsync(string userId, CancellationToken cancellationToken) =>
        context.MarketItems.Where(item => item.UserId == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.UserId, userId), cancellationToken);
}
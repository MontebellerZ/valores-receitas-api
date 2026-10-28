using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Repositories.Interfaces;

public interface IMarketItemRepository
{
    Task<List<MarketItemDto>> ListAsync(string userId, CancellationToken cancellationToken);
    Task<bool> HasForeignIdsAsync(string userId, string[] ids, CancellationToken cancellationToken);
    Task DeleteOwnedAsync(string userId, CancellationToken cancellationToken);
    void AddRange(IEnumerable<MarketItem> items);
    Task ClaimLegacyAsync(string userId, CancellationToken cancellationToken);
}

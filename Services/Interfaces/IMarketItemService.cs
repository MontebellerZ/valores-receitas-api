using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Services.Interfaces;

public interface IMarketItemService
{
    Task<List<MarketItemDto>> ListAsync(CancellationToken cancellationToken);
    void Validate(IReadOnlyCollection<MarketItemDto> items);
    IEnumerable<MarketItem> CreateModels(IEnumerable<MarketItemDto> items, string userId);
}

using ValoresReceitas.Api.Services.Interfaces;
using ValoresReceitas.Api.Repositories.Interfaces;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Services;

public sealed class MarketItemService(IMarketItemRepository repository, ICurrentUserService currentUser, IDataValidationService validationService) : IMarketItemService
{
    public Task<List<MarketItemDto>> ListAsync(CancellationToken cancellationToken) =>
        repository.ListAsync(currentUser.Id, cancellationToken);

    public void Validate(IReadOnlyCollection<MarketItemDto> items)
    {
        validationService.ValidateIds(items.Select(item => item.Id));
    }

    public IEnumerable<MarketItem> CreateModels(IEnumerable<MarketItemDto> items, string userId) =>
        items.Select(item => new MarketItem
        {
            Id = item.Id, UserId = userId, Name = item.Name, Unit = item.Unit,
        });
}
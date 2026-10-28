using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Services.Interfaces;

public interface IStockService
{
    Task<List<StockLotDto>> ListAsync(CancellationToken ct, bool includeInactive = false);
    Task<StockMutationResponseDto> UpdateAsync(UpdateStockLotsRequestDto request, CancellationToken ct);
    Task DeleteAsync(string id, long revision, CancellationToken ct);
    Task<StockLotDto?> FindAsync(string id, CancellationToken ct);
    Task<List<StockConsumptionDto>> ConsumptionsAsync(string productionId, CancellationToken ct);
    Task<StockMutationResponseDto> CreateAsync(CreateStockLotRequestDto request, CancellationToken ct);
    Task<StockMutationResponseDto> AdjustAsync(string id, AdjustStockLotRequestDto request, CancellationToken ct);
    Task ConsumeAsync(Production production, RecipeDto recipe, CancellationToken ct);
}

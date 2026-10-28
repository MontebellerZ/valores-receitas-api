using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Repositories.Interfaces;

public interface IStockRepository
{
    Task<List<StockLotDto>> ListAsync(string userId, CancellationToken ct, bool includeInactive = false);
    Task<List<StockLot>> SelectedAsync(string userId, List<string> ids, CancellationToken ct);
    Task<bool> HasConsumptionAsync(string lotId, CancellationToken ct);
    Task<bool> HasProductionUsageAsync(string lotId, CancellationToken ct);
    Task<int> PurchaseCountAsync(string userId, long entryOrder, CancellationToken ct);
    Task<DateOnly?> FirstProductionDateAsync(string lotId, CancellationToken ct);
    Task<StockLot?> FindAsync(string userId, string id, CancellationToken ct);
    Task<MarketItem?> ItemAsync(string userId, string id, CancellationToken ct);
    Task<List<StockLot>> AvailableAsync(string userId, string itemId, CancellationToken ct);
    Task<List<StockConsumptionDto>> ConsumptionsAsync(string userId, string productionId, CancellationToken ct);
    void Add(StockLot lot);
    void Remove(StockLot lot);
    void Add(StockAdjustment adjustment);
    void Add(StockConsumption consumption);
    StockLotDto ToDto(StockLot lot);
}

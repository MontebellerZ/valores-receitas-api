using ValoresReceitas.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Data;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Repositories;

public sealed class StockRepository(AppDbContext context) : IStockRepository
{
    public async Task<List<StockLotDto>> ListAsync(string userId, CancellationToken ct, bool includeInactive = false) =>
        (await context.StockLots.AsNoTracking().Where(lot => lot.UserId == userId && (includeInactive || lot.IsActive))
            .OrderBy(lot => lot.EntryDate).ThenBy(lot => lot.EntryOrder).ThenBy(lot => lot.Id).ToListAsync(ct))
        .Select(ToDto).ToList();

    public Task<List<StockLot>> SelectedAsync(string userId, List<string> ids, CancellationToken ct) =>
        context.StockLots.Where(lot => lot.UserId == userId && ids.Contains(lot.Id)).ToListAsync(ct);

    public Task<bool> HasConsumptionAsync(string lotId, CancellationToken ct) =>
        context.StockConsumptions.AnyAsync(item => item.StockLotId == lotId, ct);

    public Task<bool> HasProductionUsageAsync(string lotId, CancellationToken ct) =>
        context.StockConsumptions.AnyAsync(item => item.StockLotId == lotId, ct);

    public Task<int> PurchaseCountAsync(string userId, long entryOrder, CancellationToken ct) =>
        context.StockLots.CountAsync(lot => lot.UserId == userId && lot.EntryOrder == entryOrder, ct);

    public Task<DateOnly?> FirstProductionDateAsync(string lotId, CancellationToken ct) =>
        (from consumption in context.StockConsumptions
         join production in context.Productions.IgnoreQueryFilters() on consumption.ProductionId equals production.Id
         where consumption.StockLotId == lotId && !consumption.Reversed
         select (DateOnly?)production.Date).MinAsync(ct);

    public Task<StockLot?> FindAsync(string userId, string id, CancellationToken ct) =>
        context.StockLots.SingleOrDefaultAsync(lot => lot.Id == id && lot.UserId == userId, ct);

    public Task<MarketItem?> ItemAsync(string userId, string id, CancellationToken ct) =>
        context.MarketItems.SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, ct);

    public Task<List<StockLot>> AvailableAsync(string userId, string itemId, CancellationToken ct) =>
        context.StockLots.Where(lot => lot.UserId == userId && lot.MarketItemId == itemId && lot.IsActive)
            .OrderBy(lot => lot.EntryDate).ThenBy(lot => lot.EntryOrder).ThenBy(lot => lot.Id).ToListAsync(ct);

    public Task<List<StockConsumptionDto>> ConsumptionsAsync(string userId, string productionId, CancellationToken ct) =>
        (from consumption in context.StockConsumptions.AsNoTracking()
         join lot in context.StockLots on consumption.StockLotId equals lot.Id
         join production in context.Productions.IgnoreQueryFilters() on consumption.ProductionId equals production.Id
         where production.UserId == userId && lot.UserId == userId &&
             production.Id == productionId && !consumption.Reversed
         orderby lot.EntryDate, lot.EntryOrder, lot.Id
         select new StockConsumptionDto(lot.Id, lot.ItemName, lot.Unit, consumption.EntryDate ?? lot.EntryDate,
             consumption.Quantity, consumption.Cost, consumption.PackageQty ?? lot.PackageQty,
             consumption.PackagePrice ?? lot.PackagePrice)).ToListAsync(ct);

    public void Add(StockLot lot) => context.StockLots.Add(lot);
    public void Remove(StockLot lot)
    {
        lot.IsDeleted = true;
        lot.IsActive = false;
    }
    public void Add(StockAdjustment adjustment) => context.StockAdjustments.Add(adjustment);
    public void Add(StockConsumption consumption) => context.StockConsumptions.Add(consumption);

    public StockLotDto ToDto(StockLot lot) => new(lot.Id, lot.MarketItemId, lot.ItemName,
        lot.Unit, lot.EntryDate, lot.EntryOrder, lot.PackageQty, lot.PackagePrice, lot.RemainingQty, lot.IsActive);
}
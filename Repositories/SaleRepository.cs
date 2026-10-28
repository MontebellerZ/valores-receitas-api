using ValoresReceitas.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Data;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Repositories;

public sealed class SaleRepository(AppDbContext context) : ISaleRepository
{
    public Task<List<SaleDto>> ListAsync(string userId, CancellationToken cancellationToken) =>
        context.Sales.AsNoTracking().Where(item => item.UserId == userId)
            .OrderByDescending(item => item.Date)
            .Select(item => new SaleDto(item.Id, item.ProductionId, item.ProductionDate, item.RecipeName,
                item.Date, item.Packages, item.PricePerPackage, item.CostPerPackage,
                item.Customer, item.Paid, item.Notes, item.Donated))
            .ToListAsync(cancellationToken);

    public Task<bool> HasForeignIdsAsync(string userId, string[] ids, CancellationToken cancellationToken) =>
        context.Sales.IgnoreQueryFilters().AnyAsync(item => item.UserId != userId && ids.Contains(item.Id), cancellationToken);

    public Task DeleteOwnedAsync(string userId, CancellationToken cancellationToken) =>
        context.Sales.Where(item => item.UserId == userId).ExecuteUpdateAsync(update => update.SetProperty(entity => entity.IsDeleted, true), cancellationToken);

    public void AddRange(IEnumerable<Sale> sales) => context.Sales.AddRange(sales);

    public Task ClaimLegacyAsync(string userId, CancellationToken cancellationToken) =>
        context.Sales.Where(item => item.UserId == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.UserId, userId), cancellationToken);
}
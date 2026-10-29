using ValoresReceitas.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Data;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Repositories;

public sealed class ProductionRepository(AppDbContext context) : IProductionRepository
{
    public Task<List<ProductionDto>> ListAsync(string userId, CancellationToken cancellationToken) =>
        context.Productions.AsNoTracking().Where(item => item.UserId == userId)
            .OrderByDescending(item => item.Date)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

    public Task<List<ProductionDto>> ListByIdsAsync(string userId, string[] ids, CancellationToken cancellationToken) =>
        context.Productions.AsNoTracking().Where(item => item.UserId == userId && ids.Contains(item.Id))
            .OrderByDescending(item => item.Date)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

    private static System.Linq.Expressions.Expression<Func<Production, ProductionDto>> ToDto => item =>
        new ProductionDto(item.Id, item.Date, item.RecipeId, item.RecipeName,
            item.Quantity, item.ActualYield, item.Losses, item.Margin, item.PackageQty,
            item.YieldPerBatch, item.IngredientCostPerBatch, item.ExpectedSalePrice,
            item.TotalCost, item.IsStandalone, item.LostPackages);

    public Task<bool> HasForeignIdsAsync(string userId, string[] ids, CancellationToken cancellationToken) =>
        context.Productions.IgnoreQueryFilters().AnyAsync(item => item.UserId != userId && ids.Contains(item.Id), cancellationToken);

    public Task DeleteOwnedAsync(string userId, CancellationToken cancellationToken) =>
        context.Productions.Where(item => item.UserId == userId).ExecuteUpdateAsync(update => update.SetProperty(entity => entity.IsDeleted, true), cancellationToken);

    public void AddRange(IEnumerable<Production> productions) => context.Productions.AddRange(productions);

    public Task ClaimLegacyAsync(string userId, CancellationToken cancellationToken) =>
        context.Productions.Where(item => item.UserId == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.UserId, userId), cancellationToken);
}
using ValoresReceitas.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Data;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Repositories;

public sealed class RecipeRepository(AppDbContext context) : IRecipeRepository
{
    public Task<List<RecipeDto>> ListAsync(string userId, CancellationToken cancellationToken) =>
        context.Recipes.AsNoTracking().Where(recipe => recipe.UserId == userId)
            .OrderBy(recipe => recipe.Name)
            .Select(recipe => new RecipeDto(recipe.Id, recipe.Name, recipe.Category, recipe.Yield,
                recipe.HoursPerBatch, recipe.Ingredients.Where(item => item.UserId == userId)
                    .Select(item => new IngredientDto(item.Id, item.MarketItemId, item.Quantity)).ToList(),
                recipe.ShelfLifeDays))
            .ToListAsync(cancellationToken);

    public Task<List<RecipeDto>> ListByIdsAsync(string userId, string[] ids, CancellationToken cancellationToken) =>
        context.Recipes.AsNoTracking().Where(recipe => recipe.UserId == userId && ids.Contains(recipe.Id))
            .OrderBy(recipe => recipe.Name)
            .Select(recipe => new RecipeDto(recipe.Id, recipe.Name, recipe.Category, recipe.Yield,
                recipe.HoursPerBatch, recipe.Ingredients.Where(item => item.UserId == userId)
                .Select(item => new IngredientDto(item.Id, item.MarketItemId, item.Quantity)).ToList(),
                recipe.ShelfLifeDays))
            .ToListAsync(cancellationToken);

    public Task<bool> HasForeignIdsAsync(string userId, string[] ids, CancellationToken cancellationToken) =>
        context.Recipes.IgnoreQueryFilters().AnyAsync(item => item.UserId != userId && ids.Contains(item.Id), cancellationToken);

    public Task DeleteOwnedAsync(string userId, CancellationToken cancellationToken) =>
        context.Recipes.Where(item => item.UserId == userId).ExecuteUpdateAsync(update => update.SetProperty(entity => entity.IsDeleted, true), cancellationToken);

    public void AddRange(IEnumerable<Recipe> recipes) => context.Recipes.AddRange(recipes);

    public Task ClaimLegacyAsync(string userId, CancellationToken cancellationToken) =>
        context.Recipes.Where(item => item.UserId == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.UserId, userId), cancellationToken);
}
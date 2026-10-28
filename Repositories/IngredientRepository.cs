using ValoresReceitas.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Data;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Repositories;

public sealed class IngredientRepository(AppDbContext context) : IIngredientRepository
{
    public Task<List<IngredientDto>> ListAsync(string userId, string recipeId, CancellationToken cancellationToken) =>
        context.Ingredients.AsNoTracking().Where(item => item.UserId == userId && item.RecipeId == recipeId)
            .Where(item => context.Recipes.Any(recipe => recipe.Id == item.RecipeId && recipe.UserId == userId))
            .OrderBy(item => item.Id)
            .Select(item => new IngredientDto(item.Id, item.MarketItemId, item.Quantity))
            .ToListAsync(cancellationToken);

    public Task<bool> HasForeignIdsAsync(string userId, string[] ids, CancellationToken cancellationToken) =>
        context.Ingredients.IgnoreQueryFilters().AnyAsync(item => item.UserId != userId && ids.Contains(item.Id), cancellationToken);

    public Task DeleteOwnedAsync(string userId, CancellationToken cancellationToken) =>
        context.Ingredients.Where(item => item.UserId == userId).ExecuteUpdateAsync(update => update.SetProperty(entity => entity.IsDeleted, true), cancellationToken);

    public void AddRange(IEnumerable<Ingredient> ingredients) => context.Ingredients.AddRange(ingredients);

    public Task ClaimLegacyAsync(string userId, CancellationToken cancellationToken) =>
        context.Ingredients.Where(item => item.UserId == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.UserId, userId), cancellationToken);
}
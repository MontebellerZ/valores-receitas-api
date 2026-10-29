using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Repositories.Interfaces;

public interface IIngredientRepository
{
    Task<List<IngredientDto>> ListAsync(string userId, string recipeId, CancellationToken cancellationToken);
    Task<bool> HasForeignIdsAsync(string userId, string[] ids, CancellationToken cancellationToken);
    Task<bool> HasOwnedIdsOutsideRecipesAsync(string userId, string[] ids, string[] recipeIds, CancellationToken cancellationToken);
    Task DeleteOwnedAsync(string userId, CancellationToken cancellationToken);
    void AddRange(IEnumerable<Ingredient> ingredients);
    Task ClaimLegacyAsync(string userId, CancellationToken cancellationToken);
}

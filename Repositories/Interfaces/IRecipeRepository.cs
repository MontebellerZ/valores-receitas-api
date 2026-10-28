using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Repositories.Interfaces;

public interface IRecipeRepository
{
    Task<List<RecipeDto>> ListAsync(string userId, CancellationToken cancellationToken);
    Task<bool> HasForeignIdsAsync(string userId, string[] ids, CancellationToken cancellationToken);
    Task DeleteOwnedAsync(string userId, CancellationToken cancellationToken);
    void AddRange(IEnumerable<Recipe> recipes);
    Task ClaimLegacyAsync(string userId, CancellationToken cancellationToken);
}

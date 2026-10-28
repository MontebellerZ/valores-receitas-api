using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Services.Interfaces;

public interface IRecipeService
{
    Task<List<RecipeDto>> ListAsync(CancellationToken cancellationToken);
    void Validate(IReadOnlyCollection<RecipeDto> recipes);
    IEnumerable<Recipe> CreateModels(IEnumerable<RecipeDto> recipes, string userId);
}

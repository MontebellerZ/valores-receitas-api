using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Services.Interfaces;

public interface IIngredientService
{
    Task<List<IngredientDto>> ListAsync(string recipeId, CancellationToken cancellationToken);
    void Validate(IReadOnlyCollection<RecipeDto> recipes);
    void ValidateReferences(IEnumerable<RecipeDto> recipes, IEnumerable<MarketItemDto> market);
    IEnumerable<Ingredient> CreateModels(IEnumerable<RecipeDto> recipes, string userId);
}

using ValoresReceitas.Api.Services.Interfaces;
using ValoresReceitas.Api.Repositories.Interfaces;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Services;

public sealed class IngredientService(IIngredientRepository repository, ICurrentUserService currentUser, IDataValidationService validationService) : IIngredientService
{
    public Task<List<IngredientDto>> ListAsync(string recipeId, CancellationToken cancellationToken) =>
        repository.ListAsync(currentUser.Id, recipeId, cancellationToken);

    public void Validate(IReadOnlyCollection<RecipeDto> recipes)
    {
        validationService.Require(recipes.All(recipe => recipe.Ingredients is not null &&
            recipe.Ingredients.All(item => item is not null)), "A lista de ingredientes e obrigatoria.");
        validationService.ValidateIds(recipes.SelectMany(recipe => recipe.Ingredients).Select(item => item.Id));
        foreach (var recipe in recipes)
        {
            validationService.Require(recipe.Ingredients.All(item => item.Quantity >= 0),
                "A quantidade de ingrediente nao pode ser negativa.");
            validationService.Require(recipe.Ingredients.Select(item => item.MarketItemId)
                .Distinct(StringComparer.Ordinal).Count() == recipe.Ingredients.Count,
                "Uma receita nao pode repetir o mesmo item de mercado.");
        }
    }

    public void ValidateReferences(IEnumerable<RecipeDto> recipes, IEnumerable<MarketItemDto> market)
    {
        var marketIds = market.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var recipe in recipes)
        {
            validationService.Require(recipe.Ingredients.All(item => marketIds.Contains(item.MarketItemId)),
                $"A receita '{recipe.Name}' referencia um item de mercado inexistente.");
        }
    }

    public IEnumerable<Ingredient> CreateModels(IEnumerable<RecipeDto> recipes, string userId) =>
        recipes.SelectMany(recipe => recipe.Ingredients.Select(item => new Ingredient
        {
            Id = item.Id, UserId = userId, RecipeId = recipe.Id,
            MarketItemId = item.MarketItemId, Quantity = item.Quantity,
        }));
}
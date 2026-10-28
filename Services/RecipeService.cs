using ValoresReceitas.Api.Services.Interfaces;
using ValoresReceitas.Api.Repositories.Interfaces;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Services;

public sealed class RecipeService(IRecipeRepository repository, ICurrentUserService currentUser, IDataValidationService validationService) : IRecipeService
{
    public Task<List<RecipeDto>> ListAsync(CancellationToken cancellationToken) =>
        repository.ListAsync(currentUser.Id, cancellationToken);

    public void Validate(IReadOnlyCollection<RecipeDto> recipes)
    {
        validationService.ValidateIds(recipes.Select(recipe => recipe.Id));
        validationService.Require(recipes.All(recipe => recipe.Yield >= 0 && recipe.HoursPerBatch >= 0),
            "Rendimento e horas por lote nao podem ser negativos.");
    }

    public IEnumerable<Recipe> CreateModels(IEnumerable<RecipeDto> recipes, string userId) =>
        recipes.Select(recipe => new Recipe
        {
            Id = recipe.Id, UserId = userId, Name = recipe.Name, Category = recipe.Category,
            Yield = recipe.Yield, HoursPerBatch = recipe.HoursPerBatch,
        });
}
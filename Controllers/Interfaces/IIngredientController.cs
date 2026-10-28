using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers.Interfaces;

public interface IIngredientController
{
    Task<ActionResult<List<IngredientDto>>> Get(string recipeId, CancellationToken cancellationToken);
}

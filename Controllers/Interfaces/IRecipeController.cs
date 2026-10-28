using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers.Interfaces;

public interface IRecipeController
{
    Task<ActionResult<List<RecipeDto>>> Get(CancellationToken cancellationToken);
}

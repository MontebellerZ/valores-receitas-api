using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers.Interfaces;

public interface IIngredientController
{
    Task<ActionResult<PageDto<IngredientDto>>> Get(string recipeId, PageQueryDto query, CancellationToken cancellationToken);
}

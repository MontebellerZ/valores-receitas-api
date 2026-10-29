using ValoresReceitas.Api.Controllers.Interfaces;
using ValoresReceitas.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/recipes/{recipeId}/ingredients")]
public sealed class IngredientController(IQueryService service) : ControllerBase, IIngredientController
{
    [HttpGet]
    public async Task<ActionResult<PageDto<IngredientDto>>> Get(string recipeId, [FromQuery] PageQueryDto query, CancellationToken cancellationToken)
    {
        return Ok(await service.IngredientsAsync(recipeId, query, cancellationToken));
    }
}
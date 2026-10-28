using ValoresReceitas.Api.Controllers.Interfaces;
using ValoresReceitas.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/recipes/{recipeId}/ingredients")]
public sealed class IngredientController(IIngredientService service) : ControllerBase, IIngredientController
{
    [HttpGet]
    public async Task<ActionResult<List<IngredientDto>>> Get(string recipeId, CancellationToken cancellationToken)
    {
        return Ok(await service.ListAsync(recipeId, cancellationToken));
    }
}
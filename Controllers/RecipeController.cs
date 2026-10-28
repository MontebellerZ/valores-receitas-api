using ValoresReceitas.Api.Controllers.Interfaces;
using ValoresReceitas.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/recipes")]
public sealed class RecipeController(IRecipeService service) : ControllerBase, IRecipeController
{
    [HttpGet]
    public async Task<ActionResult<List<RecipeDto>>> Get(CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(cancellationToken));
}
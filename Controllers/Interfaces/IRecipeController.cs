using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers.Interfaces;

public interface IRecipeController
{
    Task<ActionResult<PageDto<RecipeDto>>> Get(PageQueryDto query, CancellationToken cancellationToken);
}

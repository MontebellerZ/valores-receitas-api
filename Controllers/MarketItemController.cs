using ValoresReceitas.Api.Controllers.Interfaces;
using ValoresReceitas.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/market")]
public sealed class MarketItemController(IQueryService service) : ControllerBase, IMarketItemController
{
    [HttpGet]
    public async Task<ActionResult<PageDto<MarketItemDto>>> Get([FromQuery] PageQueryDto query, CancellationToken cancellationToken) =>
        Ok(await service.MarketAsync(query, cancellationToken));
}
using ValoresReceitas.Api.Controllers.Interfaces;
using ValoresReceitas.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/market")]
public sealed class MarketItemController(IMarketItemService service) : ControllerBase, IMarketItemController
{
    [HttpGet]
    public async Task<ActionResult<List<MarketItemDto>>> Get(CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(cancellationToken));
}
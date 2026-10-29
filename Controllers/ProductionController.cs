using ValoresReceitas.Api.Controllers.Interfaces;
using ValoresReceitas.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/productions")]
public sealed class ProductionController(IQueryService service) : ControllerBase, IProductionController
{
    [HttpGet]
    public async Task<ActionResult<PageDto<ProductionDto>>> Get([FromQuery] PageQueryDto query, CancellationToken cancellationToken) =>
        Ok(await service.ProductionsAsync(query, cancellationToken));

    [HttpGet("{id}/consumptions")]
    public async Task<ActionResult<PageDto<StockConsumptionDto>>> Consumptions(string id, [FromQuery] PageQueryDto query, CancellationToken ct) =>
        Ok(await service.ConsumptionsAsync(id, query, ct));
}
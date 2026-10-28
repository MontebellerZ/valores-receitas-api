using ValoresReceitas.Api.Controllers.Interfaces;
using ValoresReceitas.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/productions")]
public sealed class ProductionController(IProductionService service, IStockService stockService) : ControllerBase, IProductionController
{
    [HttpGet]
    public async Task<ActionResult<List<ProductionDto>>> Get(CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(cancellationToken));

    [HttpGet("{id}/consumptions")]
    public async Task<ActionResult<List<StockConsumptionDto>>> Consumptions(string id, CancellationToken ct) =>
        Ok(await stockService.ConsumptionsAsync(id, ct));
}
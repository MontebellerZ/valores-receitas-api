using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers.Interfaces;

public interface IProductionController
{
    Task<ActionResult<PageDto<ProductionDto>>> Get(PageQueryDto query, CancellationToken cancellationToken);
    Task<ActionResult<PageDto<StockConsumptionDto>>> Consumptions(string id, PageQueryDto query, CancellationToken ct);
}

using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers.Interfaces;

public interface IProductionController
{
    Task<ActionResult<List<ProductionDto>>> Get(CancellationToken cancellationToken);
    Task<ActionResult<List<StockConsumptionDto>>> Consumptions(string id, CancellationToken ct);
}

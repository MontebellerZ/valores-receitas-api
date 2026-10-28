using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers.Interfaces;

public interface IMarketItemController
{
    Task<ActionResult<List<MarketItemDto>>> Get(CancellationToken cancellationToken);
}

using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers.Interfaces;

public interface IMarketItemController
{
    Task<ActionResult<PageDto<MarketItemDto>>> Get(PageQueryDto query, CancellationToken cancellationToken);
}

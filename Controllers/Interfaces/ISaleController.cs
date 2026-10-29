using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers.Interfaces;

public interface ISaleController
{
    Task<ActionResult<PageDto<SaleDto>>> Get(PageQueryDto query, CancellationToken cancellationToken);
}

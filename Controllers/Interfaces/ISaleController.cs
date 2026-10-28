using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers.Interfaces;

public interface ISaleController
{
    Task<ActionResult<List<SaleDto>>> Get(CancellationToken cancellationToken);
}

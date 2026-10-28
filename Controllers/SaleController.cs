using ValoresReceitas.Api.Controllers.Interfaces;
using ValoresReceitas.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sales")]
public sealed class SaleController(ISaleService service) : ControllerBase, ISaleController
{
    [HttpGet]
    public async Task<ActionResult<List<SaleDto>>> Get(CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(cancellationToken));
}
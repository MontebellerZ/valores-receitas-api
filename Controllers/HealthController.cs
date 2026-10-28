using ValoresReceitas.Api.Controllers.Interfaces;
using ValoresReceitas.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(IHealthService service) : ControllerBase, IHealthController
{
    [HttpGet]
    public async Task<ActionResult<HealthResponseDto>> Get(CancellationToken cancellationToken)
    {
        var response = await service.GetAsync(cancellationToken);
        return response.Status == "ok" ? Ok(response) : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}
using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers.Interfaces;

public interface IHealthController
{
    Task<ActionResult<HealthResponseDto>> Get(CancellationToken cancellationToken);
}

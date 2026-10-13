using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Data;

namespace ValoresReceitas.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var databaseAvailable = await dbContext.Database.CanConnectAsync(cancellationToken);
        return databaseAvailable
            ? Ok(new { status = "ok", database = "connected" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unavailable", database = "disconnected" });
    }
}
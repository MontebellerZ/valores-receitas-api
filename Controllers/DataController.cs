using ValoresReceitas.Api.Controllers.Interfaces;
using ValoresReceitas.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/data")]
public sealed class DataController(IDataService service) : ControllerBase, IDataController
{
    [HttpGet]
    public async Task<ActionResult<DataSnapshotDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await service.GetAsync(cancellationToken));
    }

    [HttpPut]
    public async Task<IActionResult> Replace(DataSnapshotDto snapshot, CancellationToken cancellationToken)
    {
        return Ok(await service.ReplaceAsync(snapshot, cancellationToken));
    }
}
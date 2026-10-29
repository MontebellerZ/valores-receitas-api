using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers.Interfaces;

public interface IDataController
{
    Task<ActionResult<DataSnapshotDto>> Patch(WorkspacePatchDto patch, CancellationToken cancellationToken);
    Task<ActionResult<DataSnapshotDto>> Get(CancellationToken cancellationToken);
    Task<IActionResult> Replace(DataSnapshotDto snapshot, CancellationToken cancellationToken);
}

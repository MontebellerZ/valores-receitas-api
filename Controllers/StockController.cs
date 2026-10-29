using ValoresReceitas.Api.Controllers.Interfaces;
using ValoresReceitas.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/stock")]
public sealed class StockController(IStockService service, IQueryService queryService) : ControllerBase, IStockController
{
    [HttpGet]
    public async Task<ActionResult<PageDto<StockLotDto>>> List([FromQuery] PageQueryDto query, CancellationToken ct) =>
        Ok(await queryService.StockAsync(query, ct));

    [HttpPut]
    [EndpointSummary("Corrige um pacote ou todos os pacotes do mesmo insumo cadastrados na mesma compra.")]
    [ProducesResponseType<StockMutationResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockMutationResponseDto>> Update(UpdateStockLotsRequestDto request, CancellationToken ct, [FromQuery] bool compact = false)
    {
        var result = await service.UpdateAsync(request, ct);
        return Ok(compact ? result with { Lots = null } : result);
    }

    [HttpDelete("{id}")]
    [EndpointSummary("Exclui um pacote cadastrado por engano que ainda não tem movimentações.")]
    [ProducesResponseType<WorkspaceRevisionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, [FromQuery] long revision, CancellationToken ct)
    {
        await service.DeleteAsync(id, revision, ct);
        return Ok(new WorkspaceRevisionDto(revision + 1));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StockLotDto>> Get(string id, CancellationToken ct)
    {
        var lot = await service.FindAsync(id, ct);
        return lot is null ? NotFound() : Ok(lot);
    }

    [HttpPost]
    public async Task<ActionResult<StockMutationResponseDto>> Create(CreateStockLotRequestDto request, CancellationToken ct, [FromQuery] bool compact = false)
    {
        var result = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Lot.Id }, compact ? result with { Lots = null } : result);
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<StockMutationResponseDto>> Adjust(string id, AdjustStockLotRequestDto request, CancellationToken ct) =>
        Ok(await service.AdjustAsync(id, request, ct));
}
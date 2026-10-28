using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers.Interfaces;

public interface IStockController
{
    Task<ActionResult<List<StockLotDto>>> List(CancellationToken ct, bool includeInactive = false);
    Task<ActionResult<StockMutationResponseDto>> Update(UpdateStockLotsRequestDto request, CancellationToken ct);
    Task<IActionResult> Delete(string id, long revision, CancellationToken ct);
    Task<ActionResult<StockLotDto>> Get(string id, CancellationToken ct);
    Task<ActionResult<StockMutationResponseDto>> Create(CreateStockLotRequestDto request, CancellationToken ct);
    Task<ActionResult<StockMutationResponseDto>> Adjust(string id, AdjustStockLotRequestDto request, CancellationToken ct);
}

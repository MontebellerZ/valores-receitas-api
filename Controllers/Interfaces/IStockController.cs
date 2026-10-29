using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers.Interfaces;

public interface IStockController
{
    Task<ActionResult<PageDto<StockLotDto>>> List(PageQueryDto query, CancellationToken ct);
    Task<ActionResult<StockMutationResponseDto>> Update(UpdateStockLotsRequestDto request, CancellationToken ct, bool compact = false);
    Task<IActionResult> Delete(string id, long revision, CancellationToken ct);
    Task<ActionResult<StockLotDto>> Get(string id, CancellationToken ct);
    Task<ActionResult<StockMutationResponseDto>> Create(CreateStockLotRequestDto request, CancellationToken ct, bool compact = false);
    Task<ActionResult<StockMutationResponseDto>> Adjust(string id, AdjustStockLotRequestDto request, CancellationToken ct);
}

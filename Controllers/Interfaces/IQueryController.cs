using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers.Interfaces;

public interface IQueryController
{
    Task<ActionResult<PageDto<MarketItemDto>>> Market(PageQueryDto query, CancellationToken ct);
    Task<ActionResult<PageDto<RecipeDto>>> Recipes(PageQueryDto query, CancellationToken ct);
    Task<ActionResult<PageDto<ProductionDto>>> Productions(PageQueryDto query, CancellationToken ct);
    Task<ActionResult<PageDto<SaleDto>>> Sales(PageQueryDto query, CancellationToken ct);
    Task<ActionResult<PageDto<StockLotDto>>> Stock(PageQueryDto query, CancellationToken ct);
    Task<ActionResult<PageDto<ProductionBalanceDto>>> Balances(PageQueryDto query, CancellationToken ct);
    Task<ActionResult<PageDto<StockTotalDto>>> StockTotals(PageQueryDto query, CancellationToken ct);
    Task<ActionResult<PageDto<IngredientDto>>> Ingredients(string id, PageQueryDto query, CancellationToken ct);
    Task<ActionResult<PageDto<StockConsumptionDto>>> Consumptions(string id, PageQueryDto query, CancellationToken ct);
    Task<ActionResult<MarketItemDto>> MarketItem(string id, CancellationToken ct);
    Task<ActionResult<RecipeDto>> Recipe(string id, CancellationToken ct);
    Task<ActionResult<RecipeDto>> RecipeMetadata(string id, CancellationToken ct);
    Task<ActionResult<ProductionDto>> Production(string id, CancellationToken ct);
    Task<ActionResult<SaleDto>> Sale(string id, CancellationToken ct);
    Task<ActionResult<WorkspaceSummaryDto>> Summary(PageQueryDto query, CancellationToken ct);
    Task<ActionResult<ProductionBalanceDto>> Balance(string id, CancellationToken ct);
    Task<ActionResult<ProductionReportDto>> Report(string id, PageQueryDto query, CancellationToken ct);
}

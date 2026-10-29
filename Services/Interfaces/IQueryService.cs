using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Services.Interfaces;

public interface IQueryService
{
    Task<PageDto<MarketItemDto>> MarketAsync(PageQueryDto query, CancellationToken ct);
    Task<PageDto<RecipeDto>> RecipesAsync(PageQueryDto query, CancellationToken ct);
    Task<PageDto<ProductionDto>> ProductionsAsync(PageQueryDto query, CancellationToken ct);
    Task<PageDto<SaleDto>> SalesAsync(PageQueryDto query, CancellationToken ct);
    Task<PageDto<StockLotDto>> StockAsync(PageQueryDto query, CancellationToken ct);
    Task<PageDto<ProductionBalanceDto>> BalancesAsync(PageQueryDto query, CancellationToken ct);
    Task<PageDto<StockTotalDto>> StockTotalsAsync(PageQueryDto query, CancellationToken ct);
    Task<PageDto<IngredientDto>> IngredientsAsync(string recipeId, PageQueryDto query, CancellationToken ct);
    Task<PageDto<StockConsumptionDto>> ConsumptionsAsync(string productionId, PageQueryDto query, CancellationToken ct);
    Task<MarketItemDto?> MarketItemAsync(string id, CancellationToken ct);
    Task<RecipeDto?> RecipeAsync(string id, CancellationToken ct);
    Task<RecipeDto?> RecipeMetadataAsync(string id, CancellationToken ct);
    Task<ProductionDto?> ProductionAsync(string id, CancellationToken ct);
    Task<SaleDto?> SaleAsync(string id, CancellationToken ct);
    Task<WorkspaceSummaryDto> SummaryAsync(PageQueryDto query, CancellationToken ct);
    Task<ProductionBalanceDto?> BalanceAsync(string id, CancellationToken ct);
    Task<ProductionReportDto?> ReportAsync(string id, PageQueryDto query, CancellationToken ct);
}

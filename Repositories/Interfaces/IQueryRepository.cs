using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Repositories.Interfaces;

public interface IQueryRepository
{
    Task<PageDto<MarketItemDto>> MarketAsync(string userId, PageQueryDto query, CancellationToken ct);
    Task<PageDto<RecipeDto>> RecipesAsync(string userId, PageQueryDto query, CancellationToken ct);
    Task<PageDto<ProductionDto>> ProductionsAsync(string userId, PageQueryDto query, CancellationToken ct);
    Task<PageDto<SaleDto>> SalesAsync(string userId, PageQueryDto query, CancellationToken ct);
    Task<SalesReportDto> SalesReportAsync(string userId, PageQueryDto query, CancellationToken ct);
    Task<PageDto<StockLotDto>> StockAsync(string userId, PageQueryDto query, CancellationToken ct);
    Task<PageDto<ProductionBalanceDto>> BalancesAsync(string userId, PageQueryDto query, CancellationToken ct);
    Task<PageDto<StockTotalDto>> StockTotalsAsync(string userId, PageQueryDto query, CancellationToken ct);
    Task<PageDto<IngredientDto>> IngredientsAsync(string userId, string recipeId, PageQueryDto query, CancellationToken ct);
    Task<PageDto<StockConsumptionDto>> ConsumptionsAsync(string userId, string productionId, PageQueryDto query, CancellationToken ct);
    Task<MarketItemDto?> MarketItemAsync(string userId, string id, CancellationToken ct);
    Task<RecipeDto?> RecipeAsync(string userId, string id, CancellationToken ct);
    Task<RecipeDto?> RecipeMetadataAsync(string userId, string id, CancellationToken ct);
    Task<ProductionDto?> ProductionAsync(string userId, string id, CancellationToken ct);
    Task<SaleDto?> SaleAsync(string userId, string id, CancellationToken ct);
    Task<WorkspaceSummaryDto> SummaryAsync(string userId, PageQueryDto query, CancellationToken ct);
    Task<ProductionBalanceDto?> BalanceAsync(string userId, string id, CancellationToken ct);
    Task<ProductionReportDto?> ReportAsync(string userId, string id, PageQueryDto query, CancellationToken ct);
}

using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Repositories.Interfaces;
using ValoresReceitas.Api.Services.Interfaces;

namespace ValoresReceitas.Api.Services;

public sealed class QueryService(IQueryRepository repository, ICurrentUserService currentUser) : IQueryService
{
    public Task<PageDto<MarketItemDto>> MarketAsync(PageQueryDto query, CancellationToken ct) => repository.MarketAsync(currentUser.Id, query, ct);
    public Task<PageDto<RecipeDto>> RecipesAsync(PageQueryDto query, CancellationToken ct) => repository.RecipesAsync(currentUser.Id, query, ct);
    public Task<PageDto<ProductionDto>> ProductionsAsync(PageQueryDto query, CancellationToken ct) => repository.ProductionsAsync(currentUser.Id, query, ct);
    public Task<PageDto<SaleDto>> SalesAsync(PageQueryDto query, CancellationToken ct) => repository.SalesAsync(currentUser.Id, query, ct);
    public Task<SalesReportDto> SalesReportAsync(PageQueryDto query, CancellationToken ct) => repository.SalesReportAsync(currentUser.Id, query, ct);
    public Task<PageDto<StockLotDto>> StockAsync(PageQueryDto query, CancellationToken ct) => repository.StockAsync(currentUser.Id, query, ct);
    public Task<PageDto<ProductionBalanceDto>> BalancesAsync(PageQueryDto query, CancellationToken ct) => repository.BalancesAsync(currentUser.Id, query, ct);
    public Task<PageDto<StockTotalDto>> StockTotalsAsync(PageQueryDto query, CancellationToken ct) => repository.StockTotalsAsync(currentUser.Id, query, ct);
    public Task<PageDto<IngredientDto>> IngredientsAsync(string recipeId, PageQueryDto query, CancellationToken ct) => repository.IngredientsAsync(currentUser.Id, recipeId, query, ct);
    public Task<PageDto<StockConsumptionDto>> ConsumptionsAsync(string productionId, PageQueryDto query, CancellationToken ct) => repository.ConsumptionsAsync(currentUser.Id, productionId, query, ct);
    public Task<MarketItemDto?> MarketItemAsync(string id, CancellationToken ct) => repository.MarketItemAsync(currentUser.Id, id, ct);
    public Task<RecipeDto?> RecipeAsync(string id, CancellationToken ct) => repository.RecipeAsync(currentUser.Id, id, ct);
    public Task<RecipeDto?> RecipeMetadataAsync(string id, CancellationToken ct) => repository.RecipeMetadataAsync(currentUser.Id, id, ct);
    public Task<ProductionDto?> ProductionAsync(string id, CancellationToken ct) => repository.ProductionAsync(currentUser.Id, id, ct);
    public Task<SaleDto?> SaleAsync(string id, CancellationToken ct) => repository.SaleAsync(currentUser.Id, id, ct);
    public Task<WorkspaceSummaryDto> SummaryAsync(PageQueryDto query, CancellationToken ct) => repository.SummaryAsync(currentUser.Id, query, ct);
    public Task<ProductionBalanceDto?> BalanceAsync(string id, CancellationToken ct) => repository.BalanceAsync(currentUser.Id, id, ct);
    public Task<ProductionReportDto?> ReportAsync(string id, PageQueryDto query, CancellationToken ct) => repository.ReportAsync(currentUser.Id, id, query, ct);
}

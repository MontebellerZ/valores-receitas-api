using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.Controllers.Interfaces;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Services.Interfaces;

namespace ValoresReceitas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/query")]
public sealed class QueryController(IQueryService service) : ControllerBase, IQueryController
{
    [HttpGet("market")]
    [EndpointSummary("Consulta uma página de insumos com busca e contagem de referências.")]
    public async Task<ActionResult<PageDto<MarketItemDto>>> Market([FromQuery] PageQueryDto query, CancellationToken ct) =>
        Ok(await service.MarketAsync(query, ct));

    [HttpGet("recipes")]
    [EndpointSummary("Consulta uma página de receitas sem carregar seus ingredientes.")]
    public async Task<ActionResult<PageDto<RecipeDto>>> Recipes([FromQuery] PageQueryDto query, CancellationToken ct) =>
        Ok(await service.RecipesAsync(query, ct));

    [HttpGet("productions")]
    [EndpointSummary("Consulta o histórico paginado de produções.")]
    public async Task<ActionResult<PageDto<ProductionDto>>> Productions([FromQuery] PageQueryDto query, CancellationToken ct) =>
        Ok(await service.ProductionsAsync(query, ct));

    [HttpGet("sales")]
    [EndpointSummary("Consulta vendas com filtros aplicados antes da paginação.")]
    public async Task<ActionResult<PageDto<SaleDto>>> Sales([FromQuery] PageQueryDto query, CancellationToken ct) =>
        Ok(await service.SalesAsync(query, ct));

    [HttpGet("sales-report")]
    [EndpointSummary("Indicadores de todas as vendas do período, rankings e evolução diária paginada.")]
    public async Task<ActionResult<SalesReportDto>> SalesReport([FromQuery] PageQueryDto query, CancellationToken ct) =>
        Ok(await service.SalesReportAsync(query, ct));

    [HttpGet("stock")]
    [EndpointSummary("Consulta pacotes paginados e metadados da compra completa.")]
    public async Task<ActionResult<PageDto<StockLotDto>>> Stock([FromQuery] PageQueryDto query, CancellationToken ct) =>
        Ok(await service.StockAsync(query, ct));

    [HttpGet("balances")]
    [EndpointSummary("Pagina os saldos de produção calculados sobre todas as vendas.")]
    public async Task<ActionResult<PageDto<ProductionBalanceDto>>> Balances([FromQuery] PageQueryDto query, CancellationToken ct) =>
        Ok(await service.BalancesAsync(query, ct));

    [HttpGet("stock-totals")]
    [EndpointSummary("Pagina os saldos totais de estoque agrupados por insumo.")]
    public async Task<ActionResult<PageDto<StockTotalDto>>> StockTotals([FromQuery] PageQueryDto query, CancellationToken ct) =>
        Ok(await service.StockTotalsAsync(query, ct));

    [HttpGet("recipes/{id}/ingredients")]
    [EndpointSummary("Consulta uma página dos ingredientes da receita.")]
    public async Task<ActionResult<PageDto<IngredientDto>>> Ingredients(string id, [FromQuery] PageQueryDto query, CancellationToken ct) =>
        Ok(await service.IngredientsAsync(id, query, ct));

    [HttpGet("productions/{id}/consumptions")]
    [EndpointSummary("Consulta uma página dos consumos FIFO da produção.")]
    public async Task<ActionResult<PageDto<StockConsumptionDto>>> Consumptions(string id, [FromQuery] PageQueryDto query, CancellationToken ct) =>
        Ok(await service.ConsumptionsAsync(id, query, ct));

    [HttpGet("market/{id}")]
    [EndpointSummary("Consulta um único insumo pertencente ao usuário.")]
    public async Task<ActionResult<MarketItemDto>> MarketItem(string id, CancellationToken ct) =>
        await service.MarketItemAsync(id, ct) is { } item ? Ok(item) : NotFound();

    [HttpGet("recipes/{id}")]
    [EndpointSummary("Consulta uma receita completa para edição atômica.")]
    public async Task<ActionResult<RecipeDto>> Recipe(string id, CancellationToken ct) =>
        await service.RecipeAsync(id, ct) is { } item ? Ok(item) : NotFound();

    [HttpGet("recipes/{id}/metadata")]
    [EndpointSummary("Consulta os dados da receita sem carregar ingredientes; use a listagem paginada.")]
    public async Task<ActionResult<RecipeDto>> RecipeMetadata(string id, CancellationToken ct) =>
        await service.RecipeMetadataAsync(id, ct) is { } item ? Ok(item) : NotFound();

    [HttpGet("productions/{id}")]
    [EndpointSummary("Consulta uma única produção pertencente ao usuário.")]
    public async Task<ActionResult<ProductionDto>> Production(string id, CancellationToken ct) =>
        await service.ProductionAsync(id, ct) is { } item ? Ok(item) : NotFound();

    [HttpGet("sales/{id}")]
    [EndpointSummary("Consulta uma única venda pertencente ao usuário.")]
    public async Task<ActionResult<SaleDto>> Sale(string id, CancellationToken ct) =>
        await service.SaleAsync(id, ct) is { } item ? Ok(item) : NotFound();

    [HttpGet("summary")]
    [EndpointSummary("Calcula contagens e totais globais sem carregar as coleções.")]
    public async Task<ActionResult<WorkspaceSummaryDto>> Summary([FromQuery] PageQueryDto query, CancellationToken ct) =>
        Ok(await service.SummaryAsync(query, ct));

    [HttpGet("balances/{id}")]
    [EndpointSummary("Consulta o saldo completo de uma produção.")]
    public async Task<ActionResult<ProductionBalanceDto>> Balance(string id, CancellationToken ct) =>
        await service.BalanceAsync(id, ct) is { } item ? Ok(item) : NotFound();

    [HttpGet("productions/{id}/report")]
    [EndpointSummary("Calcula totais da produção e pagina os dados diários do gráfico.")]
    public async Task<ActionResult<ProductionReportDto>> Report(string id, [FromQuery] PageQueryDto query, CancellationToken ct) =>
        await service.ReportAsync(id, query, ct) is { } item ? Ok(item) : NotFound();
}

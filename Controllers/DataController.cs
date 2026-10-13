using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Data;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Controllers;

[ApiController]
[Route("api/data")]
public sealed class DataController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DataSnapshot>> Get(CancellationToken cancellationToken)
    {
        var market = await dbContext.MarketItems.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);
        var recipes = await dbContext.Recipes.AsNoTracking().Include(recipe => recipe.Ingredients)
            .OrderBy(recipe => recipe.Name).ToListAsync(cancellationToken);
        var productions = await dbContext.Productions.AsNoTracking().OrderByDescending(item => item.Date)
            .ToListAsync(cancellationToken);
        var sales = await dbContext.Sales.AsNoTracking().OrderByDescending(item => item.Date)
            .ToListAsync(cancellationToken);

        return Ok(new DataSnapshot(market, recipes, productions, sales));
    }

    [HttpPut]
    public async Task<IActionResult> Replace(DataSnapshot snapshot, CancellationToken cancellationToken)
    {
        var validationError = Validate(snapshot);
        if (validationError is not null)
        {
            return BadRequest(new ProblemDetails { Title = "Dados inválidos", Detail = validationError, Status = 400 });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Sales.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Productions.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Ingredients.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Recipes.ExecuteDeleteAsync(cancellationToken);
        await dbContext.MarketItems.ExecuteDeleteAsync(cancellationToken);

        foreach (var recipe in snapshot.Recipes)
        {
            foreach (var ingredient in recipe.Ingredients)
            {
                ingredient.RecipeId = recipe.Id;
            }
        }

        dbContext.MarketItems.AddRange(snapshot.Market);
        dbContext.Recipes.AddRange(snapshot.Recipes);
        dbContext.Productions.AddRange(snapshot.Productions);
        dbContext.Sales.AddRange(snapshot.Sales);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return NoContent();
    }

    private static string? Validate(DataSnapshot snapshot)
    {
        if (snapshot.Market is null || snapshot.Recipes is null || snapshot.Productions is null || snapshot.Sales is null)
        {
            return "As listas market, recipes, productions e sales são obrigatórias.";
        }

        if (!HasUniqueIds(snapshot.Market.Select(item => item.Id)) ||
            !HasUniqueIds(snapshot.Recipes.Select(recipe => recipe.Id)) ||
            !HasUniqueIds(snapshot.Recipes.SelectMany(recipe => recipe.Ingredients).Select(item => item.Id)) ||
            !HasUniqueIds(snapshot.Productions.Select(item => item.Id)) ||
            !HasUniqueIds(snapshot.Sales.Select(item => item.Id)))
        {
            return "Os identificadores devem ser únicos e não podem estar vazios.";
        }

        if (snapshot.Market.Any(item => item.PackageQty <= 0 || item.PackagePrice < 0) ||
            snapshot.Recipes.Any(recipe => recipe.Yield < 0 || recipe.HoursPerBatch < 0) ||
            snapshot.Recipes.SelectMany(recipe => recipe.Ingredients).Any(item => item.Quantity < 0) ||
            snapshot.Productions.Any(item => item.Quantity < 0 || item.ActualYield < 0 || item.Losses < 0 ||
                item.Losses > item.ActualYield || item.Margin < 0 || item.PackageQty <= 0 ||
                item.IngredientCostPerBatch < 0 || item.ExpectedSalePrice < 0) ||
            snapshot.Sales.Any(item => item.Packages <= 0 || item.PricePerPackage < 0 || item.CostPerPackage < 0))
        {
            return "Quantidades, rendimentos, perdas e valores devem respeitar os limites permitidos.";
        }

        if (snapshot.Productions.Any(item => item.Date == default) || snapshot.Sales.Any(item => item.Date == default))
        {
            return "As datas de produção e venda são obrigatórias.";
        }

        var marketIds = snapshot.Market.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var recipe in snapshot.Recipes)
        {
            if (recipe.Ingredients.Any(item => !marketIds.Contains(item.MarketItemId)))
            {
                return $"A receita '{recipe.Name}' referencia um item de mercado inexistente.";
            }
        }

        var productionById = snapshot.Productions.ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var sale in snapshot.Sales)
        {
            if (!productionById.TryGetValue(sale.ProductionId, out var production))
            {
                return $"A venda '{sale.Id}' referencia uma produção inexistente.";
            }

            if (sale.Date < production.Date)
            {
                return $"A venda '{sale.Id}' deve ocorrer na data da produção ou depois dela.";
            }
        }

        foreach (var production in snapshot.Productions)
        {
            var soldPackages = snapshot.Sales.Where(sale => sale.ProductionId == production.Id).Sum(sale => sale.Packages);
            var availablePackages = (int)decimal.Floor((production.ActualYield - production.Losses) / production.PackageQty);
            if (soldPackages > availablePackages)
            {
                return $"A produção '{production.RecipeName}' não possui pacotes suficientes para as vendas informadas.";
            }
        }

        return null;
    }

    private static bool HasUniqueIds(IEnumerable<string> ids)
    {
        var values = ids.ToArray();
        return values.All(id => !string.IsNullOrWhiteSpace(id) && id.Length <= 64) &&
            values.Distinct(StringComparer.Ordinal).Count() == values.Length;
    }
}
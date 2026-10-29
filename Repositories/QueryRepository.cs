using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Data;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;
using ValoresReceitas.Api.Repositories.Interfaces;
using ValoresReceitas.Api.Services;

namespace ValoresReceitas.Api.Repositories;

public sealed class QueryRepository(AppDbContext context) : IQueryRepository
{
    private IQueryable<MarketItem> Market(string userId) => context.MarketItems.AsNoTracking().Where(item => item.UserId == userId);
    private IQueryable<Recipe> Recipes(string userId) => context.Recipes.AsNoTracking().Where(item => item.UserId == userId);
    private IQueryable<Production> Productions(string userId) => context.Productions.AsNoTracking().Where(item => item.UserId == userId);
    private IQueryable<Sale> Sales(string userId) => context.Sales.AsNoTracking().Where(item => item.UserId == userId);

    private IQueryable<MarketItemDto> ProjectMarket(IQueryable<MarketItem> items, string userId) =>
        items.Select(item => new MarketItemDto(item.Id, item.Name, item.Unit,
            context.StockLots.Any(lot => lot.UserId == userId && lot.MarketItemId == item.Id),
            context.Ingredients.Count(ingredient => ingredient.UserId == userId && ingredient.MarketItemId == item.Id)));

    private static IQueryable<RecipeDto> ProjectRecipes(IQueryable<Recipe> items, string userId, bool includeIngredients = false) =>
        items.Select(item => new RecipeDto(item.Id, item.Name, item.Category, item.Yield, item.HoursPerBatch,
            item.Ingredients.Where(ingredient => includeIngredients && ingredient.UserId == userId).OrderBy(ingredient => ingredient.Id)
                .Select(ingredient => new IngredientDto(ingredient.Id, ingredient.MarketItemId, ingredient.Quantity)).ToList(),
            item.ShelfLifeDays, item.Ingredients.Count(ingredient => ingredient.UserId == userId)));

    private static readonly Expression<Func<Production, ProductionDto>> ProductionProjection = item =>
        new ProductionDto(item.Id, item.Date, item.RecipeId, item.RecipeName, item.Quantity, item.ActualYield,
            item.Losses, item.Margin, item.PackageQty, item.YieldPerBatch, item.IngredientCostPerBatch,
            item.ExpectedSalePrice, item.TotalCost, item.IsStandalone, item.LostPackages);

    private static ProductionDto ToDto(Production item) =>
        new(item.Id, item.Date, item.RecipeId, item.RecipeName, item.Quantity, item.ActualYield,
            item.Losses, item.Margin, item.PackageQty, item.YieldPerBatch, item.IngredientCostPerBatch,
            item.ExpectedSalePrice, item.TotalCost, item.IsStandalone, item.LostPackages);

    private static readonly Expression<Func<Sale, SaleDto>> SaleProjection = item =>
        new SaleDto(item.Id, item.ProductionId, item.ProductionDate, item.RecipeName, item.Date, item.Packages,
            item.PricePerPackage, item.CostPerPackage, item.Customer, item.Paid, item.Notes, item.Donated);

    private static async Task<PageDto<T>> PageAsync<T>(IQueryable<T> items, PageQueryDto query, CancellationToken ct)
    {
        var total = await items.CountAsync(ct);
        var page = await items.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new(page, query.Page, query.PageSize, total, (int)Math.Ceiling((double)total / query.PageSize));
    }

    private static void SortAllowed(PageQueryDto query, params string[] fields)
    {
        if (!string.IsNullOrEmpty(query.SortBy) && !fields.Contains(query.SortBy, StringComparer.Ordinal))
            throw new ServiceException(ServiceError.InvalidData, "Ordenação inválida para esta listagem.");
    }

    public Task<PageDto<MarketItemDto>> MarketAsync(string userId, PageQueryDto query, CancellationToken ct)
    {
        SortAllowed(query, "name", "unit");
        var items = Market(userId);
        if (!string.IsNullOrWhiteSpace(query.Search))
            items = items.Where(item => item.Name.Contains(query.Search.Trim()) || item.Unit.Contains(query.Search.Trim()));
        if (query.Ids is not null) { var ids = query.Ids.Split(','); items = items.Where(item => ids.Contains(item.Id)); }
        var ordered = query.SortBy == "unit"
            ? query.Descending ? items.OrderByDescending(item => item.Unit) : items.OrderBy(item => item.Unit)
            : query.Descending ? items.OrderByDescending(item => item.Name) : items.OrderBy(item => item.Name);
        return PageAsync(ProjectMarket(ordered.ThenBy(item => item.Id), userId), query, ct);
    }

    public Task<PageDto<RecipeDto>> RecipesAsync(string userId, PageQueryDto query, CancellationToken ct)
    {
        SortAllowed(query, "name", "category", "yield", "hoursPerBatch");
        var items = Recipes(userId);
        if (!string.IsNullOrWhiteSpace(query.Search))
            items = items.Where(item => item.Name.Contains(query.Search.Trim()) || item.Category.Contains(query.Search.Trim()));
        if (query.MarketItemId is not null)
            items = items.Where(item => item.Ingredients.Any(ingredient => ingredient.UserId == userId && ingredient.MarketItemId == query.MarketItemId));
        if (query.Ids is not null) { var ids = query.Ids.Split(','); items = items.Where(item => ids.Contains(item.Id)); }
        var ordered = query.SortBy switch
        {
            "category" => query.Descending ? items.OrderByDescending(item => item.Category) : items.OrderBy(item => item.Category),
            "yield" => query.Descending ? items.OrderByDescending(item => (double)item.Yield) : items.OrderBy(item => (double)item.Yield),
            "hoursPerBatch" => query.Descending ? items.OrderByDescending(item => (double)item.HoursPerBatch) : items.OrderBy(item => (double)item.HoursPerBatch),
            _ => query.Descending ? items.OrderByDescending(item => item.Name) : items.OrderBy(item => item.Name),
        };
        return PageAsync(ProjectRecipes(ordered.ThenBy(item => item.Id), userId), query, ct);
    }

    private IQueryable<Production> FilterProductions(string userId, PageQueryDto query)
    {
        var items = Productions(userId);
        if (!string.IsNullOrWhiteSpace(query.Search)) items = items.Where(item => item.RecipeName.Contains(query.Search.Trim()));
        if (query.ProductionId is not null) items = items.Where(item => item.Id == query.ProductionId);
        if (query.Ids is not null) { var ids = query.Ids.Split(','); items = items.Where(item => ids.Contains(item.Id)); }
        if (query.From.HasValue) items = items.Where(item => item.Date >= query.From.Value);
        if (query.To.HasValue) items = items.Where(item => item.Date <= query.To.Value);
        return items;
    }

    private static IOrderedQueryable<Production> OrderProductions(IQueryable<Production> items, PageQueryDto query)
    {
        SortAllowed(query, "date", "recipeName", "quantity", "actualYield", "lostPackages",
            "packages", "cost", "profit");
        if (query.SortBy is "packages" or "cost" or "profit")
        {
            Expression<Func<Production, double>> key = query.SortBy switch
            {
                "packages" => item => item.Quantity > 0 && item.PackageQty > 0 && item.ActualYield > item.Losses
                    ? Math.Floor((double)(item.ActualYield - item.Losses) / (double)item.PackageQty) : 0,
                "cost" => item => (double)(item.TotalCost ?? item.IngredientCostPerBatch * item.Quantity),
                _ => item => (item.Quantity > 0 && item.PackageQty > 0 && item.ActualYield > item.Losses &&
                    Math.Floor((double)(item.ActualYield - item.Losses) / (double)item.PackageQty) > 0
                    ? (double)(item.TotalCost ?? item.IngredientCostPerBatch * item.Quantity) /
                        Math.Floor((double)(item.ActualYield - item.Losses) / (double)item.PackageQty) *
                        (1 + (item.Margin > 0 ? (double)item.Margin : 0) / 100) *
                        (Math.Floor((double)(item.ActualYield - item.Losses) / (double)item.PackageQty) > item.LostPackages
                            ? Math.Floor((double)(item.ActualYield - item.Losses) / (double)item.PackageQty) - item.LostPackages : 0)
                    : 0) - (double)(item.TotalCost ?? item.IngredientCostPerBatch * item.Quantity),
            };
            return query.Descending ? items.OrderByDescending(key) : items.OrderBy(key);
        }
        return query.SortBy switch
        {
            "recipeName" => query.Descending ? items.OrderByDescending(item => item.RecipeName) : items.OrderBy(item => item.RecipeName),
            "quantity" => query.Descending ? items.OrderByDescending(item => (double)item.Quantity) : items.OrderBy(item => (double)item.Quantity),
            "actualYield" => query.Descending ? items.OrderByDescending(item => (double)item.ActualYield) : items.OrderBy(item => (double)item.ActualYield),
            "lostPackages" => query.Descending ? items.OrderByDescending(item => item.LostPackages) : items.OrderBy(item => item.LostPackages),
            _ => query.SortBy is null || query.Descending ? items.OrderByDescending(item => item.Date) : items.OrderBy(item => item.Date),
        };
    }

    public Task<PageDto<ProductionDto>> ProductionsAsync(string userId, PageQueryDto query, CancellationToken ct) =>
        PageAsync(OrderProductions(FilterProductions(userId, query), query).ThenBy(item => item.Id).Select(ProductionProjection), query, ct);

    private IQueryable<Sale> FilterSales(string userId, PageQueryDto query)
    {
        var items = Sales(userId);
        if (!string.IsNullOrWhiteSpace(query.Search))
            items = items.Where(item => item.RecipeName.Contains(query.Search.Trim()) ||
                item.Customer.Contains(query.Search.Trim()) || item.Notes.Contains(query.Search.Trim()));
        if (query.ProductionId is not null) items = items.Where(item => item.ProductionId == query.ProductionId);
        if (query.From.HasValue) items = items.Where(item => item.Date >= query.From.Value);
        if (query.To.HasValue) items = items.Where(item => item.Date <= query.To.Value);
        if (query.Payment == "paid") items = items.Where(item => item.Paid && !item.Donated);
        if (query.Payment == "pending") items = items.Where(item => !item.Paid && !item.Donated);
        if (query.Payment == "donated") items = items.Where(item => item.Donated);
        return items;
    }

    public Task<PageDto<SaleDto>> SalesAsync(string userId, PageQueryDto query, CancellationToken ct)
    {
        SortAllowed(query, "date", "recipeName", "customer", "packages", "pricePerPackage", "paid", "total");
        var items = FilterSales(userId, query);
        var ordered = query.SortBy switch
        {
            "recipeName" => query.Descending ? items.OrderByDescending(item => item.RecipeName) : items.OrderBy(item => item.RecipeName),
            "customer" => query.Descending ? items.OrderByDescending(item => item.Customer) : items.OrderBy(item => item.Customer),
            "packages" => query.Descending ? items.OrderByDescending(item => item.Packages) : items.OrderBy(item => item.Packages),
            "pricePerPackage" => query.Descending ? items.OrderByDescending(item => (double)item.PricePerPackage) : items.OrderBy(item => (double)item.PricePerPackage),
            "paid" => query.Descending ? items.OrderByDescending(item => item.Paid) : items.OrderBy(item => item.Paid),
            "total" => query.Descending
                ? items.OrderByDescending(item => (double)item.PricePerPackage * item.Packages)
                : items.OrderBy(item => (double)item.PricePerPackage * item.Packages),
            _ => query.SortBy is null || query.Descending ? items.OrderByDescending(item => item.Date) : items.OrderBy(item => item.Date),
        };
        return PageAsync(ordered.ThenBy(item => item.Id).Select(item => new SaleDto(item.Id, item.ProductionId,
            item.ProductionDate, item.RecipeName, item.Date, item.Packages, item.PricePerPackage, item.CostPerPackage,
            item.Customer, item.Paid, item.Notes, item.Donated,
            query.ProductionId == null ? 0 : context.Sales
                .Where(previous => previous.UserId == userId && previous.ProductionId == item.ProductionId &&
                    (previous.Date < item.Date || previous.Date == item.Date && string.Compare(previous.Id, item.Id) <= 0))
                .Sum(previous => (double?)previous.PricePerPackage * previous.Packages) ?? 0)), query, ct);
    }

    private IQueryable<StockLot> FilterStock(string userId, PageQueryDto query)
    {
        var items = context.StockLots.AsNoTracking().Where(item => item.UserId == userId && (query.IncludeInactive || item.IsActive));
        if (!string.IsNullOrWhiteSpace(query.Search)) items = items.Where(item => item.ItemName.Contains(query.Search.Trim()));
        if (query.MarketItemId is not null) items = items.Where(item => item.MarketItemId == query.MarketItemId);
        if (query.EntryOrder.HasValue) items = items.Where(item => item.EntryOrder == query.EntryOrder);
        return items;
    }

    public Task<PageDto<StockLotDto>> StockAsync(string userId, PageQueryDto query, CancellationToken ct)
    {
        SortAllowed(query, "itemName", "entryDate", "entryOrder", "packageQty", "packagePrice", "remainingQty");
        var items = FilterStock(userId, query);
        var ordered = query.SortBy switch
        {
            "itemName" => query.Descending ? items.OrderByDescending(item => item.ItemName) : items.OrderBy(item => item.ItemName),
            "entryDate" => query.Descending ? items.OrderByDescending(item => item.EntryDate) : items.OrderBy(item => item.EntryDate),
            "packageQty" => query.Descending ? items.OrderByDescending(item => (double)item.PackageQty) : items.OrderBy(item => (double)item.PackageQty),
            "packagePrice" => query.Descending ? items.OrderByDescending(item => (double)item.PackagePrice) : items.OrderBy(item => (double)item.PackagePrice),
            "remainingQty" => query.Descending ? items.OrderByDescending(item => (double)item.RemainingQty) : items.OrderBy(item => (double)item.RemainingQty),
            _ => query.SortBy is null || query.Descending ? items.OrderByDescending(item => item.EntryOrder) : items.OrderBy(item => item.EntryOrder),
        };
        return PageAsync(ordered.ThenBy(item => item.Id).Select(item => new StockLotDto(item.Id, item.MarketItemId, item.ItemName,
            item.Unit, item.EntryDate, item.EntryOrder, item.PackageQty, item.PackagePrice, item.RemainingQty, item.IsActive,
            context.StockLots.Count(lot => lot.UserId == userId && lot.EntryOrder == item.EntryOrder),
            context.StockLots.Any(lot => lot.UserId == userId && lot.EntryOrder == item.EntryOrder && lot.MarketItemId != item.MarketItemId))),
            query, ct);
    }

    private sealed record BalanceRow
    {
        public required Production Production { get; init; }
        public int Sold { get; init; }
        public double Packages { get; init; }
        public double Available { get; init; }
        public double Cost { get; init; }
        public double Price { get; init; }
        public double ReferencePrice { get; init; }
        public double PeriodRevenue { get; init; }
        public double PeriodCost { get; init; }
        public int PeriodPackages { get; init; }
        public int PeriodCount { get; init; }
    }

    private IQueryable<BalanceRow> BalanceRows(string userId, PageQueryDto query)
    {
        var sold = Sales(userId).GroupBy(sale => sale.ProductionId)
            .Select(group => new { Id = group.Key, Packages = (int?)group.Sum(item => item.Packages) });
        var period = FilterSales(userId, query with { ProductionId = null, Search = null })
            .GroupBy(sale => sale.ProductionId).Select(group => new
            {
                Id = group.Key,
                Revenue = (double?)group.Sum(item => (double)item.PricePerPackage * item.Packages),
                Cost = (double?)group.Sum(item => (double)item.CostPerPackage * item.Packages),
                Packages = (int?)group.Sum(item => item.Packages),
                Count = (int?)group.Count(),
            });
        var rows = from production in FilterProductions(userId, query)
                   join total in sold on production.Id equals total.Id into totals
                   from total in totals.DefaultIfEmpty()
                   join periodTotal in period on production.Id equals periodTotal.Id into periodTotals
                   from periodTotal in periodTotals.DefaultIfEmpty()
                   let packages = production.Quantity > 0 && production.PackageQty > 0 && production.ActualYield > production.Losses
                       ? Math.Floor((double)(production.ActualYield - production.Losses) / (double)production.PackageQty) : 0
                   let cost = (double)(production.TotalCost ?? production.IngredientCostPerBatch * production.Quantity)
                   let referencePrice = packages > 0 ? cost / packages * (1 + (production.Margin > 0 ? (double)production.Margin : 0) / 100) : 0
                   let unsold = packages - production.LostPackages - (total.Packages ?? 0)
                   select new BalanceRow
                   {
                       Production = production,
                       Sold = total.Packages ?? 0,
                       Packages = packages,
                       Available = unsold > 0 ? unsold : 0,
                       Cost = cost,
                       Price = production.ExpectedSalePrice.HasValue ? (double)production.ExpectedSalePrice.GetValueOrDefault() : referencePrice,
                       ReferencePrice = referencePrice,
                       PeriodRevenue = periodTotal.Revenue ?? 0,
                       PeriodCost = periodTotal.Cost ?? 0,
                       PeriodPackages = periodTotal.Packages ?? 0,
                       PeriodCount = periodTotal.Count ?? 0,
                   };
        return query.Available ? rows.Where(item => item.Available > 0) : rows;
    }

    private async Task<Dictionary<string, PeriodSalesDto>> PeriodSalesAsync(string userId, string[] ids, PageQueryDto query, CancellationToken ct) =>
        await FilterSales(userId, query with { ProductionId = null, Search = null })
            .Where(item => ids.Contains(item.ProductionId)).GroupBy(item => item.ProductionId)
            .Select(group => new
            {
                Id = group.Key,
                Count = group.Count(),
                Packages = group.Sum(item => item.Packages),
                Revenue = group.Sum(item => (double)item.PricePerPackage * item.Packages),
                Cost = group.Sum(item => (double)item.CostPerPackage * item.Packages),
            }).ToDictionaryAsync(item => item.Id, item => new PeriodSalesDto(item.Count, item.Packages, item.Revenue, item.Cost), ct);

    public async Task<PageDto<ProductionBalanceDto>> BalancesAsync(string userId, PageQueryDto query, CancellationToken ct)
    {
        SortAllowed(query, "date", "recipeName", "packages", "sold", "available", "revenue", "profit", "cost", "lostPackages", "price", "periodSold",
            "periodPrice", "periodRevenue", "periodResult");
        var rows = BalanceRows(userId, query);
        IOrderedQueryable<BalanceRow> ordered;
        if (query.SortBy is "packages" or "sold" or "available" or "revenue" or "profit" or "cost" or "lostPackages" or "price" or "periodSold"
            or "periodPrice" or "periodRevenue" or "periodResult")
        {
            Expression<Func<BalanceRow, double>> key = query.SortBy switch
            {
                "packages" => item => item.Packages,
                "sold" => item => item.Sold,
                "periodSold" => item => item.PeriodPackages,
                "price" => item => item.Price,
                "periodPrice" => item => item.PeriodCount > 0
                    ? item.PeriodPackages > 0 ? item.PeriodRevenue / item.PeriodPackages : 0 : item.Price,
                "periodRevenue" => item => item.PeriodCount > 0 ? item.PeriodRevenue : item.Price * item.Packages,
                "periodResult" => item => item.PeriodCount > 0 ? item.PeriodRevenue - item.PeriodCost
                    : item.Packages > 0 ? item.Price * item.Packages - item.Cost : 0,
                "available" => item => item.Available,
                "cost" => item => item.Cost,
                "lostPackages" => item => item.Production.LostPackages,
                "revenue" => item => item.PeriodRevenue,
                _ => item => item.PeriodRevenue - item.PeriodCost,
            };
            ordered = query.Descending ? rows.OrderByDescending(key) : rows.OrderBy(key);
        }
        else
            ordered = query.SortBy == "recipeName"
                ? query.Descending ? rows.OrderByDescending(item => item.Production.RecipeName) : rows.OrderBy(item => item.Production.RecipeName)
                : query.SortBy is null || query.Descending ? rows.OrderByDescending(item => item.Production.Date) : rows.OrderBy(item => item.Production.Date);
        var page = await PageAsync(ordered.ThenBy(item => item.Production.Id), query, ct);
        var periodSales = await PeriodSalesAsync(userId, page.Items.Select(item => item.Production.Id).ToArray(), query, ct);
        return new(page.Items.Select(item => new ProductionBalanceDto(ToDto(item.Production), item.Sold, item.Packages, item.Available,
            periodSales.GetValueOrDefault(item.Production.Id) ?? new(0, 0, 0, 0))).ToList(),
            page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }

    public async Task<ProductionBalanceDto?> BalanceAsync(string userId, string id, CancellationToken ct) =>
        (await BalancesAsync(userId, new() { ProductionId = id }, ct)).Items.SingleOrDefault();

    public Task<PageDto<StockTotalDto>> StockTotalsAsync(string userId, PageQueryDto query, CancellationToken ct)
    {
        SortAllowed(query, "name");
        var grouped = FilterStock(userId, query).GroupBy(item => item.MarketItemId)
            .Select(group => new
            {
                Id = group.Key,
                Name = group.Min(item => item.ItemName)!,
                Unit = group.Min(item => item.Unit)!,
                Quantity = group.Sum(item => (double)item.RemainingQty),
            });
        var ordered = query.Descending ? grouped.OrderByDescending(item => item.Name) : grouped.OrderBy(item => item.Name);
        return PageAsync(ordered.ThenBy(item => item.Id).Select(item => new StockTotalDto(item.Id, item.Name, item.Unit, item.Quantity)), query, ct);
    }

    public Task<PageDto<IngredientDto>> IngredientsAsync(string userId, string recipeId, PageQueryDto query, CancellationToken ct)
    {
        SortAllowed(query, "id", "name", "unit", "quantity");
        var items = context.Ingredients.AsNoTracking().Where(item => item.UserId == userId && item.RecipeId == recipeId &&
            context.Recipes.Any(recipe => recipe.Id == recipeId && recipe.UserId == userId));
        var joined = from ingredient in items
                     join market in Market(userId) on ingredient.MarketItemId equals market.Id
                     select new { Ingredient = ingredient, market.Name, market.Unit };
        if (!string.IsNullOrWhiteSpace(query.Search))
            joined = joined.Where(item => item.Name.Contains(query.Search.Trim()) || item.Unit.Contains(query.Search.Trim()));
        var ordered = query.SortBy switch
        {
            "name" => query.Descending ? joined.OrderByDescending(item => item.Name) : joined.OrderBy(item => item.Name),
            "unit" => query.Descending ? joined.OrderByDescending(item => item.Unit) : joined.OrderBy(item => item.Unit),
            "quantity" => query.Descending ? joined.OrderByDescending(item => (double)item.Ingredient.Quantity) : joined.OrderBy(item => (double)item.Ingredient.Quantity),
            _ => query.Descending ? joined.OrderByDescending(item => item.Ingredient.Id) : joined.OrderBy(item => item.Ingredient.Id),
        };
        return PageAsync(ordered.ThenBy(item => item.Ingredient.Id)
            .Select(item => new IngredientDto(item.Ingredient.Id, item.Ingredient.MarketItemId, item.Ingredient.Quantity)), query, ct);
    }

    public Task<PageDto<StockConsumptionDto>> ConsumptionsAsync(string userId, string productionId, PageQueryDto query, CancellationToken ct)
    {
        SortAllowed(query, "entryDate", "itemName", "unit", "quantity", "cost", "packageQty", "packagePrice");
        var items = from consumption in context.StockConsumptions.AsNoTracking()
                    join lot in context.StockLots on consumption.StockLotId equals lot.Id
                    join production in context.Productions.IgnoreQueryFilters() on consumption.ProductionId equals production.Id
                    where production.UserId == userId && lot.UserId == userId &&
                        production.Id == productionId && !consumption.Reversed
                    select new
                    {
                        lot.Id,
                        lot.ItemName,
                        lot.Unit,
                        lot.EntryOrder,
                        EntryDate = consumption.EntryDate ?? lot.EntryDate,
                        consumption.Quantity,
                        consumption.Cost,
                        PackageQty = consumption.PackageQty ?? lot.PackageQty,
                        PackagePrice = consumption.PackagePrice ?? lot.PackagePrice
                    };
        if (!string.IsNullOrWhiteSpace(query.Search)) items = items.Where(item => item.ItemName.Contains(query.Search.Trim()));
        var ordered = query.SortBy switch
        {
            "itemName" => query.Descending ? items.OrderByDescending(item => item.ItemName) : items.OrderBy(item => item.ItemName),
            "unit" => query.Descending ? items.OrderByDescending(item => item.Unit) : items.OrderBy(item => item.Unit),
            "quantity" => query.Descending ? items.OrderByDescending(item => (double)item.Quantity) : items.OrderBy(item => (double)item.Quantity),
            "cost" => query.Descending ? items.OrderByDescending(item => (double)item.Cost) : items.OrderBy(item => (double)item.Cost),
            "packageQty" => query.Descending ? items.OrderByDescending(item => (double)item.PackageQty) : items.OrderBy(item => (double)item.PackageQty),
            "packagePrice" => query.Descending ? items.OrderByDescending(item => (double)item.PackagePrice) : items.OrderBy(item => (double)item.PackagePrice),
            _ => query.Descending ? items.OrderByDescending(item => item.EntryDate) : items.OrderBy(item => item.EntryDate),
        };
        return PageAsync(ordered.ThenBy(item => item.EntryOrder).ThenBy(item => item.Id).Select(item => new StockConsumptionDto(item.Id, item.ItemName, item.Unit,
            item.EntryDate, item.Quantity, item.Cost, item.PackageQty, item.PackagePrice)), query, ct);
    }

    public Task<MarketItemDto?> MarketItemAsync(string userId, string id, CancellationToken ct) =>
        ProjectMarket(Market(userId).Where(item => item.Id == id), userId).SingleOrDefaultAsync(ct);
    public Task<RecipeDto?> RecipeAsync(string userId, string id, CancellationToken ct) =>
        ProjectRecipes(Recipes(userId).Where(item => item.Id == id), userId, includeIngredients: true).SingleOrDefaultAsync(ct);
    public Task<RecipeDto?> RecipeMetadataAsync(string userId, string id, CancellationToken ct) =>
        ProjectRecipes(Recipes(userId).Where(item => item.Id == id), userId).SingleOrDefaultAsync(ct);
    public Task<ProductionDto?> ProductionAsync(string userId, string id, CancellationToken ct) =>
        Productions(userId).Where(item => item.Id == id).Select(ProductionProjection).SingleOrDefaultAsync(ct);
    public Task<SaleDto?> SaleAsync(string userId, string id, CancellationToken ct) =>
        Sales(userId).Where(item => item.Id == id).Select(SaleProjection).SingleOrDefaultAsync(ct);

    public async Task<WorkspaceSummaryDto> SummaryAsync(string userId, PageQueryDto query, CancellationToken ct)
    {
        var counts = new WorkspaceCountsDto(await Market(userId).CountAsync(ct), await Recipes(userId).CountAsync(ct),
            await Productions(userId).CountAsync(ct), await Sales(userId).CountAsync(ct));
        var sales = FilterSales(userId, query);
        var totals = await sales.GroupBy(item => 1).Select(group => new SalesTotalsDto(
            group.Sum(item => item.Packages), group.Sum(item => (double)item.PricePerPackage * item.Packages),
            group.Sum(item => item.Paid ? (double)item.PricePerPackage * item.Packages : 0),
            group.Sum(item => item.Paid ? 0 : (double)item.PricePerPackage * item.Packages),
            group.Sum(item => (double)item.CostPerPackage * item.Packages),
            group.Sum(item => ((double)item.PricePerPackage - (double)item.CostPerPackage) * item.Packages),
            group.Count(), group.Count(item => !item.Paid)))
            .SingleOrDefaultAsync(ct) ?? new(0, 0, 0, 0, 0, 0);
        var rows = BalanceRows(userId, query);
        var production = await rows.GroupBy(item => 1).Select(group => new
        {
            Count = group.Count(),
            Sold = group.Sum(item => item.Sold),
            Losses = group.Sum(item => (double)item.Production.Losses),
            Available = group.Sum(item => item.Available),
            Produced = group.Sum(item => item.Packages),
            Lost = group.Sum(item => item.Production.LostPackages),
            Cost = group.Sum(item => item.Cost),
            RemainingRevenue = group.Sum(item => item.Price * item.Available),
            RemainingCost = group.Sum(item => item.Packages > 0 ? item.Cost / item.Packages * item.Available : 0),
            ExpectedRevenue = group.Sum(item => item.Price * (item.Packages > item.Production.LostPackages ? item.Packages - item.Production.LostPackages : 0)),
            ReferenceRevenue = group.Sum(item => item.ReferencePrice * (item.Packages > item.Production.LostPackages ? item.Packages - item.Production.LostPackages : 0)),
        }).SingleOrDefaultAsync(ct);
        var revision = await context.Users.Where(item => item.Id == userId).Select(item => item.WorkspaceRevision).SingleAsync(ct);
        return new(revision, counts, totals, production?.Available ?? 0, production?.Cost ?? 0, production?.Produced ?? 0, production?.Lost ?? 0,
            new(production?.Count ?? 0, production?.Sold ?? 0, production?.Losses ?? 0,
                production?.RemainingRevenue ?? 0, production?.RemainingCost ?? 0, production?.ExpectedRevenue ?? 0,
                production?.ReferenceRevenue ?? 0, (production?.ReferenceRevenue ?? 0) - (production?.Cost ?? 0)));
    }

    public async Task<SalesReportDto> SalesReportAsync(string userId, PageQueryDto query, CancellationToken ct)
    {
        var all = FilterSales(userId, query with { Search = null, Payment = null });
        var purchases = all.Where(item => !item.Donated);
        var totals = await all.GroupBy(item => 1).Select(group => new SalesReportTotalsDto(
            group.Count(item => !item.Donated),
            group.Sum(item => item.Donated ? 0 : item.Packages),
            group.Sum(item => item.Donated ? item.Packages : 0),
            group.Count(item => !item.Donated && !item.Paid),
            0,
            group.Sum(item => item.Donated ? 0 : (double)item.PricePerPackage * item.Packages),
            group.Sum(item => !item.Donated && item.Paid ? (double)item.PricePerPackage * item.Packages : 0),
            group.Sum(item => !item.Donated && !item.Paid ? (double)item.PricePerPackage * item.Packages : 0),
            group.Sum(item => item.Donated ? 0 : (double)item.CostPerPackage * item.Packages),
            group.Sum(item => item.Donated ? (double)item.CostPerPackage * item.Packages : 0)))
            .SingleOrDefaultAsync(ct) ?? new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var customers = await purchases.Where(item => item.Customer.Trim() != "")
            .Select(item => item.Customer.Trim().ToUpper()).Distinct().CountAsync(ct);
        var daily = await PageAsync(purchases.GroupBy(item => item.Date).OrderBy(group => group.Key)
            .Select(group => new SalesReportDayDto(group.Key, group.Sum(item => item.Packages),
                group.Sum(item => (double)item.PricePerPackage * item.Packages))), query, ct);
        var products = await purchases.GroupBy(item => item.RecipeName)
            .Select(group => new
            {
                Name = group.Key, Packages = group.Sum(item => item.Packages),
                Revenue = group.Sum(item => (double)item.PricePerPackage * item.Packages),
                Cost = group.Sum(item => (double)item.CostPerPackage * item.Packages),
            })
            .OrderByDescending(item => item.Revenue).ThenByDescending(item => item.Packages).ThenBy(item => item.Name)
            .Take(5).Select(item => new SalesProductDto(item.Name, item.Packages, item.Revenue, item.Cost)).ToListAsync(ct);
        var buyers = await purchases.GroupBy(item => item.Customer.Trim().ToUpper())
            .Select(group => new
            {
                Customer = group.Min(item => item.Customer.Trim())!,
                Packages = group.Sum(item => item.Packages),
                Spent = group.Sum(item => (double)item.PricePerPackage * item.Packages),
            })
            .OrderByDescending(item => item.Spent).ThenByDescending(item => item.Packages).ThenBy(item => item.Customer)
            .Take(5).Select(item => new ProductionBuyerDto(item.Customer, item.Packages, item.Spent)).ToListAsync(ct);
        return new(totals with { Customers = customers }, daily, products, buyers);
    }

    public async Task<ProductionReportDto?> ReportAsync(string userId, string id, PageQueryDto query, CancellationToken ct)
    {
        if (!await Productions(userId).AnyAsync(item => item.Id == id, ct)) return null;
        var sales = Sales(userId).Where(item => item.ProductionId == id);
        var totals = await sales.GroupBy(item => 1).Select(group => new
        {
            Count = group.Count(),
            Out = group.Sum(item => item.Packages),
            Donated = group.Sum(item => item.Donated ? item.Packages : 0),
            Revenue = group.Sum(item => (double)item.PricePerPackage * item.Packages),
            Received = group.Sum(item => item.Paid ? (double)item.PricePerPackage * item.Packages : 0),
            Pending = group.Sum(item => item.Paid ? 0 : (double)item.PricePerPackage * item.Packages),
            Cost = group.Sum(item => (double)item.CostPerPackage * item.Packages),
            FirstDate = group.Min(item => (DateOnly?)item.Date),
        }).SingleOrDefaultAsync(ct);
        var dailyRows = await PageAsync(sales.GroupBy(item => item.Date).OrderBy(group => group.Key)
            .Select(group => new
            {
                Date = group.Key,
                Sold = group.Sum(item => item.Donated ? 0 : item.Packages),
                Donated = group.Sum(item => item.Donated ? item.Packages : 0),
                Total = group.Sum(item => item.Packages),
                Revenue = group.Sum(item => (double)item.PricePerPackage * item.Packages),
            }), query, ct);
        var cumulative = dailyRows.Items.Count > 0
            ? await sales.Where(item => item.Date < dailyRows.Items[0].Date)
                .SumAsync(item => (double?)item.PricePerPackage * item.Packages, ct) ?? 0
            : 0;
        var days = new List<ProductionDayDto>(dailyRows.Items.Count);
        foreach (var day in dailyRows.Items)
        {
            cumulative += day.Revenue;
            days.Add(new(day.Date, day.Sold, day.Donated, day.Total, day.Revenue, cumulative));
        }
        var daily = new PageDto<ProductionDayDto>(days, dailyRows.Page, dailyRows.PageSize, dailyRows.TotalCount, dailyRows.TotalPages);
        var topBuyers = await sales.Where(item => !item.Donated)
            .GroupBy(item => item.Customer.Trim().ToUpper())
            .Select(group => new
            {
                Customer = group.Min(item => item.Customer.Trim())!,
                Packages = group.Sum(item => item.Packages),
                Spent = group.Sum(item => (double)item.PricePerPackage * item.Packages),
            })
            .OrderByDescending(item => item.Packages).ThenByDescending(item => item.Spent).ThenBy(item => item.Customer)
            .Take(5)
            .Select(item => new ProductionBuyerDto(item.Customer, item.Packages, item.Spent))
            .ToListAsync(ct);
        return new(totals?.Count ?? 0, totals?.Out ?? 0, totals?.Donated ?? 0, totals?.Revenue ?? 0,
            totals?.Received ?? 0, totals?.Pending ?? 0, totals?.Cost ?? 0, totals?.FirstDate, daily, topBuyers);
    }
}

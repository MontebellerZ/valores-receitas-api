namespace ValoresReceitas.Api.Models;

public sealed class MarketItem
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Unit { get; set; }
    public decimal PackageQty { get; set; }
    public decimal PackagePrice { get; set; }
    public List<Ingredient> Ingredients { get; set; } = [];
}

public sealed class Recipe
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public decimal Yield { get; set; }
    public decimal HoursPerBatch { get; set; }
    public List<Ingredient> Ingredients { get; set; } = [];
}

public sealed class Ingredient
{
    public required string Id { get; set; }
    public string RecipeId { get; set; } = string.Empty;
    public required string MarketItemId { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class Production
{
    public required string Id { get; set; }
    public DateOnly Date { get; set; }
    public required string RecipeId { get; set; }
    public required string RecipeName { get; set; }
    public decimal Quantity { get; set; }
    public decimal ActualYield { get; set; }
    public decimal Losses { get; set; }
    public decimal Margin { get; set; }
    public decimal PackageQty { get; set; }
    public decimal YieldPerBatch { get; set; }
    public decimal IngredientCostPerBatch { get; set; }
    public decimal? ExpectedSalePrice { get; set; }
}

public sealed class Sale
{
    public required string Id { get; set; }
    public required string ProductionId { get; set; }
    public DateOnly ProductionDate { get; set; }
    public required string RecipeName { get; set; }
    public DateOnly Date { get; set; }
    public int Packages { get; set; }
    public decimal PricePerPackage { get; set; }
    public decimal CostPerPackage { get; set; }
    public required string Customer { get; set; }
    public bool Paid { get; set; }
    public required string Notes { get; set; }
}

public sealed record DataSnapshot(
    List<MarketItem> Market,
    List<Recipe> Recipes,
    List<Production> Productions,
    List<Sale> Sales);
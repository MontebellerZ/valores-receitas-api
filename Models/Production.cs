namespace ValoresReceitas.Api.Models;

public sealed class Production : ISoftDeletable
{
    public required string Id { get; set; }
    public string? UserId { get; set; }
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
    public decimal? TotalCost { get; set; }
    public int LostPackages { get; set; }
    public bool IsStandalone { get; set; }
    public bool IsDeleted { get; set; }
}
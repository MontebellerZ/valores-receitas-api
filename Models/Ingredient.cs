namespace ValoresReceitas.Api.Models;

public sealed class Ingredient : ISoftDeletable
{
    public required string Id { get; set; }
    public bool IsDeleted { get; set; }
    public string? UserId { get; set; }
    public string RecipeId { get; set; } = string.Empty;
    public required string MarketItemId { get; set; }
    public decimal Quantity { get; set; }
}
namespace ValoresReceitas.Api.Models;

public sealed class MarketItem : ISoftDeletable
{
    public required string Id { get; set; }
    public bool IsDeleted { get; set; }
    public string? UserId { get; set; }
    public required string Name { get; set; }
    public required string Unit { get; set; }
    public decimal PackageQty { get; set; }
    public decimal PackagePrice { get; set; }
    public List<Ingredient> Ingredients { get; set; } = [];
}
namespace ValoresReceitas.Api.Models;

public sealed class Recipe : ISoftDeletable
{
    public required string Id { get; set; }
    public bool IsDeleted { get; set; }
    public string? UserId { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public decimal Yield { get; set; }
    public decimal HoursPerBatch { get; set; }
    public List<Ingredient> Ingredients { get; set; } = [];
}
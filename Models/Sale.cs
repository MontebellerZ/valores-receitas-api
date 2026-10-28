namespace ValoresReceitas.Api.Models;

public sealed class Sale : ISoftDeletable
{
    public required string Id { get; set; }
    public bool IsDeleted { get; set; }
    public string? UserId { get; set; }
    public required string ProductionId { get; set; }
    public DateOnly ProductionDate { get; set; }
    public required string RecipeName { get; set; }
    public DateOnly Date { get; set; }
    public int Packages { get; set; }
    public decimal PricePerPackage { get; set; }
    public decimal CostPerPackage { get; set; }
    public required string Customer { get; set; }
    public bool Paid { get; set; }
    public bool Donated { get; set; }
    public required string Notes { get; set; }
}
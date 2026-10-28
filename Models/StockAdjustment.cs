namespace ValoresReceitas.Api.Models;

public sealed class StockAdjustment : ISoftDeletable
{
    public required string Id { get; set; }
    public bool IsDeleted { get; set; }
    public required string StockLotId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public decimal PreviousQty { get; set; }
    public decimal RemainingQty { get; set; }
    public required string Reason { get; set; }
    public string? PreviousData { get; set; }
    public string? CorrectedData { get; set; }
}
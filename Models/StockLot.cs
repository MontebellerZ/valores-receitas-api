namespace ValoresReceitas.Api.Models;

public sealed class StockLot : ISoftDeletable
{
    public required string Id { get; set; }
    public required string UserId { get; set; }
    public required string MarketItemId { get; set; }
    public required string ItemName { get; set; }
    public required string Unit { get; set; }
    public DateOnly EntryDate { get; set; }
    public long EntryOrder { get; set; }
    public decimal PackageQty { get; set; }
    public decimal PackagePrice { get; set; }
    public decimal RemainingQty { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}
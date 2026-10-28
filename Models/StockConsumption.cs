namespace ValoresReceitas.Api.Models;

public sealed class StockConsumption : ISoftDeletable
{
    public required string Id { get; set; }
    public bool IsDeleted { get; set; }
    public required string ProductionId { get; set; }
    public required string StockLotId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Cost { get; set; }
    public bool Reversed { get; set; }
    public DateOnly? EntryDate { get; set; }
    public decimal? PackageQty { get; set; }
    public decimal? PackagePrice { get; set; }
}
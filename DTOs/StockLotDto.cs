namespace ValoresReceitas.Api.DTOs;

/// <summary>Pacote comprado e seu saldo na unidade do insumo.</summary>
public sealed record StockLotDto(string Id, string MarketItemId, string ItemName, string Unit,
    DateOnly EntryDate, long EntryOrder, decimal PackageQty, decimal PackagePrice, decimal RemainingQty, bool IsActive);

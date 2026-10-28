namespace ValoresReceitas.Api.DTOs;

/// <summary>Rastreabilidade do custo efetivamente consumido por uma producao.</summary>
public sealed record StockConsumptionDto(string StockLotId, string ItemName, string Unit,
    DateOnly EntryDate, decimal Quantity, decimal Cost, decimal PackageQty, decimal PackagePrice);

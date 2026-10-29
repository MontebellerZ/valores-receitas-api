namespace ValoresReceitas.Api.DTOs;

/// <summary>Available stock grouped by catalog item.</summary>
public sealed record StockTotalDto(string Id, string Name, string Unit, double Quantity);

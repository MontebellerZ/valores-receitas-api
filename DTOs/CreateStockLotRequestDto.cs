using System.ComponentModel.DataAnnotations;

namespace ValoresReceitas.Api.DTOs;

/// <summary>Entrada de pacotes, com quantidade e preco individuais preservados em cada lote.</summary>
public sealed record CreateStockLotRequestDto
{
    [Required, MaxLength(64)] public required string MarketItemId { get; init; }
    public DateOnly EntryDate { get; init; }
    [Range(1, int.MaxValue)]
    public int PackageCount { get; init; } = 1;
    [Range(typeof(decimal), "0.0001", "99999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal PackageQty { get; init; }
    [Range(typeof(decimal), "0", "9999999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal PackagePrice { get; init; }
    public long Revision { get; init; }
}

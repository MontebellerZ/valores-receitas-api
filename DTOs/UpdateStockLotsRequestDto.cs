using System.ComponentModel.DataAnnotations;

namespace ValoresReceitas.Api.DTOs;

/// <summary>Correcao de um pacote ou da compra completa de um mesmo insumo. Campos omitidos sao preservados.</summary>
public sealed record UpdateStockLotsRequestDto
{
    [Required, MinLength(1), MaxLength(1000)]
    public required List<string> LotIds { get; init; }
    [MaxLength(64)] public string? MarketItemId { get; init; }
    public DateOnly? EntryDate { get; init; }
    [Range(typeof(decimal), "0.0001", "99999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? PackageQty { get; init; }
    [Range(typeof(decimal), "0", "9999999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? PackagePrice { get; init; }
    [Required, MaxLength(500)] public required string Reason { get; init; }
    public long Revision { get; init; }
}

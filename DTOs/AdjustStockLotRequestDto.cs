using System.ComponentModel.DataAnnotations;

namespace ValoresReceitas.Api.DTOs;

/// <summary>Baixa manual: informa o saldo restante e o motivo, sem excluir o lote.</summary>
public sealed record AdjustStockLotRequestDto
{
    public decimal RemainingQty { get; init; }
    [Required, MaxLength(500)] public required string Reason { get; init; }
    public long Revision { get; init; }
}

namespace ValoresReceitas.Api.DTOs;

/// <summary>Resultado de uma alteracao no estoque e versao atual do workspace.</summary>
public sealed record StockMutationResponseDto(StockLotDto Lot, long Revision, IReadOnlyList<StockLotDto>? Lots = null);

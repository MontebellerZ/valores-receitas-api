using System.ComponentModel.DataAnnotations;

namespace ValoresReceitas.Api.DTOs;

/// <summary>Contrato de sincronizacao atomica do workspace do usuario autenticado.</summary>
public sealed record DataSnapshotDto(
    [Required] List<MarketItemDto> Market,
    [Required] List<RecipeDto> Recipes,
    [Required] List<ProductionDto> Productions,
    [Required] List<SaleDto> Sales,
    long? Revision = null);
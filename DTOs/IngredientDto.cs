using System.ComponentModel.DataAnnotations;

namespace ValoresReceitas.Api.DTOs;

/// <summary>Ingrediente no contexto de uma receita, sem dados de propriedade.</summary>
public sealed record IngredientDto(
    [Required, MaxLength(64)] string Id,
    [Required, MaxLength(64)] string MarketItemId,
    decimal Quantity);
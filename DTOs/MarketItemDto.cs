using System.ComponentModel.DataAnnotations;

namespace ValoresReceitas.Api.DTOs;

/// <summary>Insumo cadastrado, sem dados de embalagem ou preco de compra.</summary>
public sealed record MarketItemDto(
    [Required, MaxLength(64)] string Id,
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(32)] string Unit,
    bool HasStockHistory = false,
    int IngredientCount = 0);
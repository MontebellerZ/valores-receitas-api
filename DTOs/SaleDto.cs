using System.ComponentModel.DataAnnotations;

namespace ValoresReceitas.Api.DTOs;

/// <summary>Registro de venda necessario ao acompanhamento financeiro.</summary>
public sealed record SaleDto(
    [Required, MaxLength(64)] string Id,
    [Required, MaxLength(64)] string ProductionId,
    DateOnly ProductionDate,
    [Required, MaxLength(200)] string RecipeName,
    DateOnly Date,
    int Packages,
    decimal PricePerPackage,
    decimal CostPerPackage,
    [MaxLength(200)] string Customer,
    bool Paid,
    [MaxLength(2000)] string Notes,
    bool Donated = false);
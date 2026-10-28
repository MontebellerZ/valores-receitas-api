using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ValoresReceitas.Api.DTOs;

/// <summary>Registro historico de uma producao usado pelo workspace.</summary>
/// <remarks>Producoes avulsas (<c>IsStandalone</c>) nao consomem estoque: o custo total e informado manualmente em <c>TotalCost</c>.
/// <c>LostPackages</c> sao pacotes prontos perdidos depois da producao (vencidos, roubados) e nao podem ser vendidos.</remarks>
public sealed record ProductionDto(
    [Required, MaxLength(64)] string Id,
    DateOnly Date,
    [Required, MaxLength(64)] string RecipeId,
    [Required, MaxLength(200)] string RecipeName,
    decimal Quantity,
    decimal ActualYield,
    decimal Losses,
    decimal Margin,
    decimal PackageQty,
    decimal YieldPerBatch,
    decimal IngredientCostPerBatch,
    decimal? ExpectedSalePrice,
    decimal? TotalCost = null,
    bool IsStandalone = false,
    int LostPackages = 0)
{
    [JsonIgnore]
    public decimal PackageCount => PackageQty > 0 ? decimal.Floor((ActualYield - Losses) / PackageQty) : 0;
}
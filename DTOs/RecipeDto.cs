using System.ComponentModel.DataAnnotations;

namespace ValoresReceitas.Api.DTOs;

/// <summary>Receita e os ingredientes necessarios ao calculo de custos.</summary>
public sealed record RecipeDto(
    [Required, MaxLength(64)] string Id,
    [Required, MaxLength(200)] string Name,
    [Required(AllowEmptyStrings = true), MaxLength(100)] string Category,
    decimal Yield,
    decimal HoursPerBatch,
    [Required] List<IngredientDto> Ingredients,
    int? ShelfLifeDays = null,
    int IngredientCount = 0);
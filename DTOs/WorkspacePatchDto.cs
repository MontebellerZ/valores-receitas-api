namespace ValoresReceitas.Api.DTOs;

/// <summary>Changed records and explicit deletions, never a replacement of an unloaded collection.</summary>
public sealed record WorkspacePatchDto(
    List<MarketItemDto> Market,
    List<RecipeDto> Recipes,
    List<ProductionDto> Productions,
    List<SaleDto> Sales,
    WorkspaceDeletionsDto Deleted,
    long Revision,
    bool PartialRecipeIngredients = false,
    List<string>? DeletedIngredients = null);

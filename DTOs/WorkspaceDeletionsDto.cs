namespace ValoresReceitas.Api.DTOs;

/// <summary>Identifiers explicitly removed by a workspace mutation.</summary>
public sealed record WorkspaceDeletionsDto(
    List<string> Market, List<string> Recipes, List<string> Productions, List<string> Sales);

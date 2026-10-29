using System.ComponentModel.DataAnnotations;

namespace ValoresReceitas.Api.DTOs;

/// <summary>Bounded, server-side list query. Filters and sorting run before pagination.</summary>
public sealed record PageQueryDto : IValidatableObject
{
    [Range(1, 20000000)]
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    [MaxLength(200)]
    public string? Search { get; init; }
    [MaxLength(64)]
    public string? SortBy { get; init; }
    public bool Descending { get; init; }
    [MaxLength(64)]
    public string? ProductionId { get; init; }
    [MaxLength(64)]
    public string? MarketItemId { get; init; }
    public bool IncludeInactive { get; init; }
    public bool Available { get; init; }
    [MaxLength(6499)]
    public string? Ids { get; init; }
    public long? EntryOrder { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    [RegularExpression("^(paid|pending|donated)$")]
    public string? Payment { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PageSize is not (10 or 25 or 50 or 100))
            yield return new("Escolha 10, 25, 50 ou 100 itens por página.", [nameof(PageSize)]);
        if (From > To)
            yield return new("O início do período deve ser anterior ao fim.", [nameof(From)]);
        if (Ids is not null && (Ids.Split(',').Length > 100 ||
            Ids.Split(',').Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 64)))
            yield return new("Informe até 100 identificadores válidos.", [nameof(Ids)]);
    }
}

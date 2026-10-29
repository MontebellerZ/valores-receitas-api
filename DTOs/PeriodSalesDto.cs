namespace ValoresReceitas.Api.DTOs;

/// <summary>Sales for one production within the selected period.</summary>
public sealed record PeriodSalesDto(int Count, int Packages, double Revenue, double Cost);

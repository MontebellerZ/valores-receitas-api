namespace ValoresReceitas.Api.DTOs;

/// <summary>One day's sales for a production.</summary>
public sealed record ProductionDayDto(DateOnly Date, int Sold, int Donated, int Total, double Revenue, double CumulativeRevenue);

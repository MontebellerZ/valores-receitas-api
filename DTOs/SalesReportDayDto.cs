namespace ValoresReceitas.Api.DTOs;

public sealed record SalesReportDayDto(DateOnly Date, int Packages, double Revenue);

namespace ValoresReceitas.Api.DTOs;

/// <summary>Production report with bounded daily chart data and complete aggregates.</summary>
public sealed record ProductionReportDto(int SalesCount, int OutPackages, int DonatedPackages, double Revenue,
    double Received, double Pending, double SoldCost, DateOnly? FirstSaleDate, PageDto<ProductionDayDto> Daily,
    IReadOnlyList<ProductionBuyerDto> TopBuyers);

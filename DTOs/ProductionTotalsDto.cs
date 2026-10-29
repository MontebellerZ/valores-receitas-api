namespace ValoresReceitas.Api.DTOs;

/// <summary>Production totals and projections independent of the visible page.</summary>
public sealed record ProductionTotalsDto(int Count, double SoldPackages, double Losses, double RemainingRevenue,
    double RemainingCost, double ExpectedRevenue, double ReferenceRevenue, double ReferenceProfit, double DonatedPackages = 0);

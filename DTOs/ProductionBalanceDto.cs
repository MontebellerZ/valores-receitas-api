namespace ValoresReceitas.Api.DTOs;

/// <summary>Production with balances calculated from all its sales, not just a page.</summary>
public sealed record ProductionBalanceDto(ProductionDto Production, int SoldPackages, double Packages, double AvailablePackages,
    PeriodSalesDto PeriodSales);

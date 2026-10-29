namespace ValoresReceitas.Api.DTOs;

/// <summary>Financial totals calculated over the complete matching period.</summary>
public sealed record SalesTotalsDto(double Packages, double Revenue, double Received, double Pending, double Cost, double Profit,
    int Count = 0, int PendingCount = 0);

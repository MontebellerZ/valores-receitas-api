namespace ValoresReceitas.Api.DTOs;

public sealed record SalesReportTotalsDto(int Count, int Packages, int DonatedPackages, int PendingCount,
    int Customers, double Revenue, double Received, double Pending, double Cost, double DonationCost);

namespace ValoresReceitas.Api.DTOs;

/// <summary>Small workspace metadata and global aggregates, independent of list pagination.</summary>
public sealed record WorkspaceSummaryDto(long Revision, WorkspaceCountsDto Counts, SalesTotalsDto Sales,
    double AvailablePackages, double ProductionCost, double ProducedPackages, double LostPackages, ProductionTotalsDto Productions);

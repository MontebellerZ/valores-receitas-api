namespace ValoresReceitas.Api.DTOs;

/// <summary>A bounded page, including the total number of matching records.</summary>
public sealed record PageDto<T>(List<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

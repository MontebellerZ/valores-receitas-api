namespace ValoresReceitas.Api.DTOs;

public sealed record SalesReportDto(SalesReportTotalsDto Totals, PageDto<SalesReportDayDto> Daily,
    IReadOnlyList<SalesProductDto> TopProducts, IReadOnlyList<ProductionBuyerDto> TopBuyers);

using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Services.Interfaces;

public interface ISaleService
{
    Task<List<SaleDto>> ListAsync(CancellationToken cancellationToken);
    void Validate(IReadOnlyCollection<SaleDto> sales);
    void ValidateReferences(IEnumerable<SaleDto> sales, IEnumerable<ProductionDto> productions,
        IReadOnlyDictionary<string, decimal>? existingPackages = null);
    IEnumerable<Sale> CreateModels(IEnumerable<SaleDto> sales, string userId);
}

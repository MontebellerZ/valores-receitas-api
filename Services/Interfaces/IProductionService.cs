using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Services.Interfaces;

public interface IProductionService
{
    Task<List<ProductionDto>> ListAsync(CancellationToken cancellationToken);
    void Validate(IReadOnlyCollection<ProductionDto> productions);
    IEnumerable<Production> CreateModels(IEnumerable<ProductionDto> productions, string userId);
}

using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Repositories.Interfaces;

public interface IProductionRepository
{
    Task<List<ProductionDto>> ListAsync(string userId, CancellationToken cancellationToken);
    Task<List<ProductionDto>> ListByIdsAsync(string userId, string[] ids, CancellationToken cancellationToken);
    Task<bool> HasForeignIdsAsync(string userId, string[] ids, CancellationToken cancellationToken);
    Task DeleteOwnedAsync(string userId, CancellationToken cancellationToken);
    void AddRange(IEnumerable<Production> productions);
    Task ClaimLegacyAsync(string userId, CancellationToken cancellationToken);
}

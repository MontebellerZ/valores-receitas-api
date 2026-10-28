using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Repositories.Interfaces;

public interface ISaleRepository
{
    Task<List<SaleDto>> ListAsync(string userId, CancellationToken cancellationToken);
    Task<bool> HasForeignIdsAsync(string userId, string[] ids, CancellationToken cancellationToken);
    Task DeleteOwnedAsync(string userId, CancellationToken cancellationToken);
    void AddRange(IEnumerable<Sale> sales);
    Task ClaimLegacyAsync(string userId, CancellationToken cancellationToken);
}

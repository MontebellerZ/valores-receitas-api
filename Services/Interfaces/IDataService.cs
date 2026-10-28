using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Services.Interfaces;

public interface IDataService
{
    Task<DataSnapshotDto> GetAsync(CancellationToken cancellationToken);
    Task<DataSnapshotDto> ReplaceAsync(DataSnapshotDto snapshot, CancellationToken cancellationToken);
    void Validate(DataSnapshotDto snapshot);
    Task ClaimLegacyAsync(string userId, CancellationToken cancellationToken);
}

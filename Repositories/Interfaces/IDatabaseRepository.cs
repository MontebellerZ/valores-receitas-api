namespace ValoresReceitas.Api.Repositories.Interfaces;

public interface IDatabaseRepository
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
    Task MigrateAsync(CancellationToken cancellationToken = default);
}

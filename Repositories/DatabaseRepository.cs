using ValoresReceitas.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Data;

namespace ValoresReceitas.Api.Repositories;

public sealed class DatabaseRepository(AppDbContext context) : IDatabaseRepository
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
        context.Database.CanConnectAsync(cancellationToken);

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        context.Database.MigrateAsync(cancellationToken);
}
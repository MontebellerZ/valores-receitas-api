using ValoresReceitas.Api.Repositories.Interfaces;
using System.Data;
using Microsoft.EntityFrameworkCore;
using ValoresReceitas.Api.Data;

namespace ValoresReceitas.Api.Repositories;

public sealed class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);

    public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            await action();
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            context.ChangeTracker.Clear();
            throw;
        }
    }
}
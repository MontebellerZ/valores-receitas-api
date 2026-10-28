namespace ValoresReceitas.Api.Repositories.Interfaces;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken);
}

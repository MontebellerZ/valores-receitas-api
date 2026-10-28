using ValoresReceitas.Api.Services.Interfaces;
using ValoresReceitas.Api.Repositories.Interfaces;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Services;

public sealed class HealthService(IDatabaseRepository repository) : IHealthService
{
    public async Task<HealthResponseDto> GetAsync(CancellationToken cancellationToken)
    {
        var available = await repository.CanConnectAsync(cancellationToken);
        return available ? new HealthResponseDto("ok", "connected") : new HealthResponseDto("unavailable", "disconnected");
    }
}
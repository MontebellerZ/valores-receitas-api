using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Services.Interfaces;

public interface IHealthService
{
    Task<HealthResponseDto> GetAsync(CancellationToken cancellationToken);
}

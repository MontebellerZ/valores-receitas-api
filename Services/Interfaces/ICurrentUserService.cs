using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Services.Interfaces;

public interface ICurrentUserService
{
    string Id { get; }

    SessionResponseDto GetSession();
}

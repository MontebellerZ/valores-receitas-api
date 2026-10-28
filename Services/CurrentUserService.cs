using ValoresReceitas.Api.Services.Interfaces;
using System.Security.Claims;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Services;

public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    public string Id => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new ServiceException(ServiceError.Unauthorized, "Sessao nao encontrada.");

    public SessionResponseDto GetSession() => new(Id,
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name)
        ?? throw new ServiceException(ServiceError.Unauthorized, "Sessao nao encontrada."));
}
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Services.Interfaces;

public interface IUserService
{
    Task<MessageResponseDto> RegisterAsync(RegisterRequestDto request);
    Task<MessageResponseDto> ResendConfirmationAsync(EmailRequestDto request);
    Task ConfirmEmailAsync(ConfirmEmailRequestDto request);
    Task<SessionResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken);
    SessionResponseDto GetSession();
    Task LogoutAsync();
}

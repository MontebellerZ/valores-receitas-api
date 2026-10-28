using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Controllers.Interfaces;

public interface IUserController
{
    Task<ActionResult<MessageResponseDto>> Register(RegisterRequestDto request);
    Task<ActionResult<MessageResponseDto>> ResendConfirmation(EmailRequestDto request);
    Task<IActionResult> ConfirmEmail(ConfirmEmailRequestDto request);
    Task<ActionResult<SessionResponseDto>> Login(LoginRequestDto request, CancellationToken cancellationToken);
    ActionResult<SessionResponseDto> Session();
    Task<IActionResult> Logout();
}

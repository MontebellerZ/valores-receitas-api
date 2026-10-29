using ValoresReceitas.Api.Controllers.Interfaces;
using ValoresReceitas.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Services;

namespace ValoresReceitas.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class UserController(IUserService service) : ControllerBase, IUserController
{
    [AllowAnonymous]
    [HttpPost("register")]
    [EnableRateLimiting(AuthRateLimitPolicies.Register)]
    public async Task<ActionResult<MessageResponseDto>> Register(RegisterRequestDto request) =>
        Accepted(await service.RegisterAsync(request));

    [AllowAnonymous]
    [HttpPost("resend-confirmation")]
    [EnableRateLimiting(AuthRateLimitPolicies.ResendConfirmation)]
    public async Task<ActionResult<MessageResponseDto>> ResendConfirmation(EmailRequestDto request) =>
        Accepted(await service.ResendConfirmationAsync(request));

    [AllowAnonymous]
    [HttpPost("confirm-email")]
    [EnableRateLimiting(AuthRateLimitPolicies.ConfirmEmail)]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequestDto request)
    {
        await service.ConfirmEmailAsync(request);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(AuthRateLimitPolicies.Login)]
    public async Task<ActionResult<SessionResponseDto>> Login(LoginRequestDto request, CancellationToken cancellationToken) =>
        Ok(await service.LoginAsync(request, cancellationToken));

    [Authorize]
    [HttpGet("session")]
    public ActionResult<SessionResponseDto> Session() => Ok(service.GetSession());

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await service.LogoutAsync();
        return NoContent();
    }
}
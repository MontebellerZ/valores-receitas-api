using ValoresReceitas.Api.Services.Interfaces;
using ValoresReceitas.Api.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Settings;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Services;

public sealed class UserService(
    IUserRepository repository,
    IDataService dataService,
    ICurrentUserService currentUser,
    IEmailSender<AccountUser> emailSender,
    IOptions<FrontendSettings> frontendSettings,
    ILogger<UserService> logger) : IUserService
{
    public async Task<MessageResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        var user = new AccountUser { UserName = request.Email.Trim(), Email = request.Email.Trim() };
        var result = await repository.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.GroupBy(error => error.Code)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
            throw new ServiceException(ServiceError.InvalidData, "Nao foi possivel criar a conta.", errors: errors);
        }
        await SendConfirmationAsync(user, isRegistration: true);
        return new MessageResponseDto("Enviamos um link de confirmacao para o seu email.");
    }

    public async Task<MessageResponseDto> ResendConfirmationAsync(EmailRequestDto request)
    {
        var user = await repository.FindByEmailAsync(request.Email.Trim());
        if (user is not null && !user.EmailConfirmed)
        {
            await SendConfirmationAsync(user, isRegistration: false);
        }
        return new MessageResponseDto("Se a conta existir e precisar de confirmacao, enviaremos um novo link.");
    }

    public async Task ConfirmEmailAsync(ConfirmEmailRequestDto request)
    {
        var user = await repository.FindByIdAsync(request.UserId);
        if (user is null || !(await repository.ConfirmEmailAsync(user, request.Token)).Succeeded)
        {
            throw new ServiceException(ServiceError.InvalidData, "Link de confirmacao invalido ou expirado.");
        }
    }

    public async Task<SessionResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var user = await repository.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            throw InvalidCredentials();
        }
        if (!user.EmailConfirmed)
        {
            throw new ServiceException(ServiceError.Forbidden, "Confirme seu email antes de entrar.", code: "email_not_confirmed");
        }
        var result = await repository.SignInAsync(user, request.Password);
        if (result.IsLockedOut)
        {
            throw new ServiceException(ServiceError.Locked, "Muitas tentativas. Tente novamente em alguns minutos.");
        }
        if (!result.Succeeded)
        {
            throw InvalidCredentials();
        }
        await dataService.ClaimLegacyAsync(user.Id, cancellationToken);
        return new SessionResponseDto(user.Id, user.Email!);
    }

    public SessionResponseDto GetSession() => currentUser.GetSession();

    public Task LogoutAsync() => repository.SignOutAsync();

    private static ServiceException InvalidCredentials() =>
        new(ServiceError.Unauthorized, "Email ou senha invalidos.");

    private async Task SendConfirmationAsync(AccountUser user, bool isRegistration)
    {
        try
        {
            var token = await repository.GenerateConfirmationTokenAsync(user);
            var link = QueryHelpers.AddQueryString(frontendSettings.Value.BaseUrl, new Dictionary<string, string?>
            {
                ["confirmUserId"] = user.Id,
                ["confirmToken"] = token,
            });
            await emailSender.SendConfirmationLinkAsync(user, user.Email!, link);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Falha ao enviar o email de confirmacao para {Email}.", user.Email);
            throw new ServiceException(ServiceError.Unavailable,
                isRegistration ? "Conta criada, mas o email nao foi enviado." : "Nao foi possivel enviar o email agora.",
                isRegistration ? "Configure o SMTP e solicite o reenvio da confirmacao." : "Verifique a configuracao SMTP e tente novamente.");
        }
    }
}
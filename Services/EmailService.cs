using ValoresReceitas.Api.Services.Interfaces;
using System.Text.Encodings.Web;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MimeKit;
using ValoresReceitas.Api.Models;
using ValoresReceitas.Api.Settings;

namespace ValoresReceitas.Api.Services;

public sealed class EmailService(IOptions<EmailSettings> settings) : IEmailSender<AccountUser>, IEmailService
{
    public Task SendConfirmationLinkAsync(AccountUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirme seu email no Conta de Chef", confirmationLink, "Confirmar email");

    public Task SendPasswordResetLinkAsync(AccountUser user, string email, string resetLink) =>
        SendAsync(email, "Redefina sua senha do Conta de Chef", resetLink, "Redefinir senha");

    public Task SendPasswordResetCodeAsync(AccountUser user, string email, string resetCode) =>
        SendAsync(email, "Código para redefinir sua senha", resetCode, "Usar este código");

    private async Task SendAsync(string email, string subject, string content, string actionLabel)
    {
        var options = settings.Value;
        if (string.IsNullOrWhiteSpace(options.Host) || string.IsNullOrWhiteSpace(options.FromAddress))
        {
            throw new InvalidOperationException("O envio de email ainda não foi configurado.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.SenderName, options.FromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        var encodedContent = HtmlEncoder.Default.Encode(content);
        message.Body = new BodyBuilder
        {
            TextBody = $"{actionLabel}: {content}",
            HtmlBody = $"<p>Use o link abaixo para continuar:</p><p><a href=\"{encodedContent}\">{actionLabel}</a></p>",
        }.ToMessageBody();

        using var client = new MailKit.Net.Smtp.SmtpClient();
        var secureSocketOptions = options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(options.Host, options.Port, secureSocketOptions);
        if (!string.IsNullOrWhiteSpace(options.UserName))
        {
            await client.AuthenticateAsync(options.UserName, options.Password);
        }
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
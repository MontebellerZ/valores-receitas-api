using Microsoft.AspNetCore.Identity;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Services.Interfaces;

public interface IEmailService : IEmailSender<AccountUser>
{
}

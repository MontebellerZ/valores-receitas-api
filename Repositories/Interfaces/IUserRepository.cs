using Microsoft.AspNetCore.Identity;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Repositories.Interfaces;

public interface IUserRepository
{
    Task<IdentityResult> CreateAsync(AccountUser user, string password);
    Task<AccountUser?> FindByEmailAsync(string email);
    Task<AccountUser?> FindByIdAsync(string id);
    Task<string> GenerateConfirmationTokenAsync(AccountUser user);
    Task<IdentityResult> ConfirmEmailAsync(AccountUser user, string token);
    Task<SignInResult> SignInAsync(AccountUser user, string password);
    Task SignOutAsync();
}

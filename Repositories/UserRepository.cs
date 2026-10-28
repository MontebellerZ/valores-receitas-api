using ValoresReceitas.Api.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Repositories;

public sealed class UserRepository(UserManager<AccountUser> userManager, SignInManager<AccountUser> signInManager) : IUserRepository
{
    public Task<IdentityResult> CreateAsync(AccountUser user, string password) => userManager.CreateAsync(user, password);

    public Task<AccountUser?> FindByEmailAsync(string email) => userManager.FindByEmailAsync(email);

    public Task<AccountUser?> FindByIdAsync(string id) => userManager.FindByIdAsync(id);

    public Task<string> GenerateConfirmationTokenAsync(AccountUser user) => userManager.GenerateEmailConfirmationTokenAsync(user);

    public Task<IdentityResult> ConfirmEmailAsync(AccountUser user, string token) => userManager.ConfirmEmailAsync(user, token);

    public Task<SignInResult> SignInAsync(AccountUser user, string password) =>
        signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);

    public Task SignOutAsync() => signInManager.SignOutAsync();
}
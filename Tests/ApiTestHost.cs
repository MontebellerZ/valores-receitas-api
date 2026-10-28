using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValoresReceitas.Api.Controllers;
using ValoresReceitas.Api.Data;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Settings;
using ValoresReceitas.Api.Middleware;
using ValoresReceitas.Api.Models;
using ValoresReceitas.Api.Services;
using Xunit;

namespace ValoresReceitas.Api.Tests;

internal sealed class ApiTestHost(WebApplication app, SqliteConnection connection) : IAsyncDisposable
{
    public const string Password = "Test-only.123";
    public TestEmailSender EmailSender => app.Services.GetRequiredService<TestEmailSender>();

    public static async Task<ApiTestHost> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Error);
        builder.Logging.AddFilter((_, level) => level >= LogLevel.Error);
        builder.Services.AddControllers().AddApplicationPart(typeof(DataController).Assembly);
        builder.Services.AddApplicationServices();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ServiceExceptionHandler>();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        builder.Services.AddIdentityCore<AccountUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
        }).AddEntityFrameworkStores<AppDbContext>().AddSignInManager().AddDefaultTokenProviders();
        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        builder.Services.AddAuthorization();
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
        });
        builder.Services.Configure<FrontendSettings>(options => options.BaseUrl = "http://localhost");
        builder.Services.AddSingleton<TestEmailSender>();
        builder.Services.AddSingleton<IEmailSender<AccountUser>>(provider => provider.GetRequiredService<TestEmailSender>());
        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        }
        await app.StartAsync();
        return new ApiTestHost(app, connection);
    }

    public HttpClient CreateClient() => app.GetTestClient();

    public async Task<HttpClient> CreateUserAsync(string email, bool confirmed = true)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var result = await manager.CreateAsync(new AccountUser
        {
            UserName = email, Email = email, EmailConfirmed = confirmed,
        }, Password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
        var client = CreateClient();
        if (confirmed)
        {
            await LoginAsync(client, email);
        }
        return client;
    }

    public static async Task LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = email, Password = Password });
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", response.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0]));
    }

    public async Task WithDatabaseAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task WithServicesAsync(Func<IServiceProvider, Task> action)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    public async ValueTask DisposeAsync()
    {
        await app.DisposeAsync();
        await connection.DisposeAsync();
    }
}

internal sealed class TestEmailSender : IEmailSender<AccountUser>
{
    public string? ConfirmationLink { get; private set; }
    public bool Fail { get; set; }

    public Task SendConfirmationLinkAsync(AccountUser user, string email, string confirmationLink)
    {
        if (Fail)
        {
            throw new InvalidOperationException("SMTP indisponivel no teste.");
        }
        ConfirmationLink = confirmationLink;
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(AccountUser user, string email, string resetLink) => Task.CompletedTask;
    public Task SendPasswordResetCodeAsync(AccountUser user, string email, string resetCode) => Task.CompletedTask;
}
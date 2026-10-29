using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ValoresReceitas.Api.Controllers;
using ValoresReceitas.Api.Controllers.Interfaces;
using ValoresReceitas.Api.Models;
using ValoresReceitas.Api.Repositories;
using ValoresReceitas.Api.Repositories.Interfaces;
using ValoresReceitas.Api.Services.Interfaces;
using System.Globalization;
using System.Threading.RateLimiting;

namespace ValoresReceitas.Api.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, int loginRateLimit = 10)
    {
        services.AddHttpContextAccessor();
        services.AddApplicationRateLimiting(loginRateLimit);
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IDataValidationService, DataValidationService>();
        services.AddScoped<IMarketItemService, MarketItemService>();
        services.AddScoped<IRecipeService, RecipeService>();
        services.AddScoped<IIngredientService, IngredientService>();
        services.AddScoped<IProductionService, ProductionService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IDataService, DataService>();
        services.AddScoped<IQueryService, QueryService>();
        services.AddScoped<IQueryRepository, QueryRepository>();
        services.AddScoped<IQueryController, QueryController>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IHealthService, HealthService>();
        services.AddTransient<IEmailService, EmailService>();
        services.AddTransient<IEmailSender<AccountUser>>(provider => provider.GetRequiredService<IEmailService>());
        services.AddScoped<IMarketItemRepository, MarketItemRepository>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<IIngredientRepository, IngredientRepository>();
        services.AddScoped<IProductionRepository, ProductionRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDatabaseRepository, DatabaseRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IDataController, DataController>();
        services.AddScoped<IHealthController, HealthController>();
        services.AddScoped<IIngredientController, IngredientController>();
        services.AddScoped<IMarketItemController, MarketItemController>();
        services.AddScoped<IProductionController, ProductionController>();
        services.AddScoped<IRecipeController, RecipeController>();
        services.AddScoped<ISaleController, SaleController>();
        services.AddScoped<IStockController, StockController>();
        services.AddScoped<IUserController, UserController>();
        return services;
    }

    public static IServiceCollection AddApplicationRateLimiting(this IServiceCollection services, int loginRateLimit = 10)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            AddPolicy(options, AuthRateLimitPolicies.Login, loginRateLimit, TimeSpan.FromMinutes(1));
            AddPolicy(options, AuthRateLimitPolicies.Register, 5, TimeSpan.FromHours(1));
            AddPolicy(options, AuthRateLimitPolicies.ConfirmEmail, 10, TimeSpan.FromMinutes(1));
            AddPolicy(options, AuthRateLimitPolicies.ResendConfirmation, 3, TimeSpan.FromHours(1));
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Muitas requisições. Tente novamente em alguns instantes.",
                };
                var problemDetailsService = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetailsService.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = problemDetails,
                });
            };
        });
        return services;
    }

    private static void AddPolicy(
        RateLimiterOptions options, string name, int permitLimit, TimeSpan window) =>
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = permitLimit,
                QueueLimit = 0,
                Window = window,
            }));
}

public static class AuthRateLimitPolicies
{
    public const string Login = "auth-login";
    public const string Register = "auth-register";
    public const string ConfirmEmail = "auth-confirm-email";
    public const string ResendConfirmation = "auth-resend-confirmation";
}
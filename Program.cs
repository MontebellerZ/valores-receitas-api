using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using ValoresReceitas.Api.Settings;
using Scalar.AspNetCore;
using ValoresReceitas.Api.Data;
using ValoresReceitas.Api.Models;
using ValoresReceitas.Api.Middleware;
using ValoresReceitas.Api.Repositories.Interfaces;
using ValoresReceitas.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (allowedOrigins is null || allowedOrigins.Length == 0 || allowedOrigins.Any(string.IsNullOrWhiteSpace))
{
    throw new InvalidOperationException("Configure Cors:AllowedOrigins no appsettings.json com pelo menos uma origem não vazia.");
}

var crossSiteCookies = builder.Configuration.GetValue<bool>("Authentication:CrossSiteCookies");

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplicationServices();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ServiceExceptionHandler>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentityCore<AccountUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.AddAuthorization();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "conta-de-chef.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = crossSiteCookies ? SameSiteMode.None : SameSiteMode.Lax;
    options.Cookie.SecurePolicy = crossSiteCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<FrontendSettings>(builder.Configuration.GetSection("Frontend"));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/swagger", options => options.WithTitle("Conta de Chef API"));
}

app.UseExceptionHandler();
app.UseCors();

if (crossSiteCookies)
{
    app.Use(async (context, next) =>
    {
        var method = context.Request.Method;
        if (context.Request.Path.StartsWithSegments("/api") &&
            !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method))
        {
            var origin = context.Request.Headers.Origin.ToString();
            var sameOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
            if (string.IsNullOrEmpty(origin) ||
                (!allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase) &&
                 !string.Equals(origin, sameOrigin, StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { title = "Origem não autorizada.", status = 403 });
                return;
            }
        }

        await next(context);
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<IDatabaseRepository>();
    await database.MigrateAsync();
}

app.Run();

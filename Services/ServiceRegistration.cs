using Microsoft.AspNetCore.Identity;
using ValoresReceitas.Api.Controllers;
using ValoresReceitas.Api.Controllers.Interfaces;
using ValoresReceitas.Api.Models;
using ValoresReceitas.Api.Repositories;
using ValoresReceitas.Api.Repositories.Interfaces;
using ValoresReceitas.Api.Services.Interfaces;

namespace ValoresReceitas.Api.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
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
}
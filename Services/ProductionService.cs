using ValoresReceitas.Api.Services.Interfaces;
using ValoresReceitas.Api.Repositories.Interfaces;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Services;

public sealed class ProductionService(IProductionRepository repository, ICurrentUserService currentUser, IDataValidationService validationService) : IProductionService
{
    public Task<List<ProductionDto>> ListAsync(CancellationToken cancellationToken) =>
        repository.ListAsync(currentUser.Id, cancellationToken);

    public void Validate(IReadOnlyCollection<ProductionDto> productions)
    {
        validationService.ValidateIds(productions.Select(item => item.Id));
        validationService.Require(productions.All(item => item.Quantity >= 0 && item.ActualYield >= 0 &&
            item.Losses >= 0 && item.Losses <= item.ActualYield && item.Margin >= 0 && item.PackageQty > 0 &&
            item.IngredientCostPerBatch >= 0 && (item.ExpectedSalePrice is null || item.ExpectedSalePrice >= 0)),
            "Quantidades, rendimentos, perdas e valores da producao devem respeitar os limites permitidos.");
        validationService.Require(productions.All(item => item.LostPackages >= 0 && item.LostPackages <= item.PackageCount),
            "Os pacotes perdidos nao podem ser negativos nem maiores que os pacotes produzidos.");
        validationService.Require(productions.All(item => item.Date != default),
            "As datas de producao sao obrigatorias.");
        validationService.Require(productions.Where(item => item.IsStandalone).All(item => item.Quantity > 0 &&
            item.TotalCost is >= 0 && decimal.Round(item.TotalCost.Value, 2) == item.TotalCost),
            "Informe a quantidade de lotes e o custo total (ate 2 casas decimais) da producao avulsa.");
    }

    public IEnumerable<Production> CreateModels(IEnumerable<ProductionDto> productions, string userId) =>
        productions.Select(item => new Production
        {
            Id = item.Id, UserId = userId, Date = item.Date, RecipeId = item.RecipeId,
            RecipeName = item.RecipeName, Quantity = item.Quantity, ActualYield = item.ActualYield,
            Losses = item.Losses, Margin = item.Margin, PackageQty = item.PackageQty,
            YieldPerBatch = item.YieldPerBatch, IngredientCostPerBatch = item.IngredientCostPerBatch,
            ExpectedSalePrice = item.ExpectedSalePrice, TotalCost = item.TotalCost, IsStandalone = item.IsStandalone,
            LostPackages = item.LostPackages,
        });
}
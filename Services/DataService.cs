using ValoresReceitas.Api.Services.Interfaces;
using ValoresReceitas.Api.Repositories.Interfaces;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Services;

public sealed class DataService(
    ICurrentUserService currentUser,
    IMarketItemService marketService,
    IRecipeService recipeService,
    IIngredientService ingredientService,
    IProductionService productionService,
    ISaleService saleService,
    IMarketItemRepository marketRepository,
    IRecipeRepository recipeRepository,
    IIngredientRepository ingredientRepository,
    IProductionRepository productionRepository,
    ISaleRepository saleRepository,
    IWorkspaceRepository workspaceRepository,
    IStockService stockService,
    IUnitOfWork unitOfWork,
    IDataValidationService validationService) : IDataService
{
    public async Task<DataSnapshotDto> GetAsync(CancellationToken cancellationToken)
    {
        DataSnapshotDto? snapshot = null;
        await unitOfWork.ExecuteAsync(async () =>
        {
            snapshot = await ReadSnapshotAsync(cancellationToken);
        }, cancellationToken);
        return snapshot!;
    }

    private async Task<DataSnapshotDto> ReadSnapshotAsync(CancellationToken ct)
    {
        var revision = await workspaceRepository.RevisionAsync(currentUser.Id, ct);
        return new(await marketService.ListAsync(ct), await recipeService.ListAsync(ct),
            await productionService.ListAsync(ct), await saleService.ListAsync(ct), revision);
    }

    public async Task<DataSnapshotDto> ReplaceAsync(DataSnapshotDto snapshot, CancellationToken cancellationToken)
    {
        Validate(snapshot);
        var userId = currentUser.Id;
        DataSnapshotDto? response = null;
        await unitOfWork.ExecuteAsync(async () =>
        {
            await workspaceRepository.AdvanceRevisionAsync(userId, snapshot.Revision, cancellationToken);
            await EnsureOwnershipAsync(snapshot, userId, cancellationToken);
            var existing = (await workspaceRepository.ProductionsAsync(userId, cancellationToken))
                .ToDictionary(item => item.Id, StringComparer.Ordinal);
            var saved = new Dictionary<string, Models.Production>(StringComparer.Ordinal);
            foreach (var model in productionService.CreateModels(snapshot.Productions, userId))
            {
                if (existing.Remove(model.Id, out var previous))
                {
                    validationService.Require(!previous.IsDeleted, "Uma produção removida não pode ser reutilizada.");
                    if (previous.Quantity > 0)
                    {
                        validationService.Require(previous.RecipeId == model.RecipeId && previous.Quantity == model.Quantity,
                            "Receita e quantidade de uma produção salva são imutáveis. Registre uma nova produção.");
                        validationService.Require(previous.IsStandalone == model.IsStandalone,
                            "O tipo de uma produção salva (normal ou avulsa) não pode ser alterado.");
                        validationService.Require(previous.PackageQty == model.PackageQty ||
                            !snapshot.Sales.Any(sale => sale.ProductionId == model.Id),
                            "Uma produção com vendas não pode mudar a quantidade por pacote.");
                        previous.Date = model.Date;
                        previous.ActualYield = model.ActualYield;
                        previous.Losses = model.Losses;
                        previous.Margin = model.Margin;
                        previous.PackageQty = model.PackageQty;
                        previous.ExpectedSalePrice = model.ExpectedSalePrice;
                        previous.LostPackages = model.LostPackages;
                        if (previous.IsStandalone) ApplyManualCost(previous, model.TotalCost!.Value);
                        saved.Add(previous.Id, previous);
                        continue;
                    }
                    previous.RecipeId = model.RecipeId;
                    previous.RecipeName = model.RecipeName;
                    previous.Date = model.Date;
                    previous.Quantity = model.Quantity;
                    previous.ActualYield = model.ActualYield;
                    previous.Losses = model.Losses;
                    previous.Margin = model.Margin;
                    previous.PackageQty = model.PackageQty;
                    previous.ExpectedSalePrice = model.ExpectedSalePrice;
                    previous.YieldPerBatch = model.YieldPerBatch;
                    previous.IsStandalone = model.IsStandalone;
                    previous.TotalCost = model.TotalCost;
                    previous.LostPackages = model.LostPackages;
                    await PriceProductionAsync(previous, snapshot, cancellationToken);
                    saved.Add(previous.Id, previous);
                }
                else
                {
                    await PriceProductionAsync(model, snapshot, cancellationToken);
                    workspaceRepository.AddProduction(model);
                    saved.Add(model.Id, model);
                }
            }
            foreach (var removed in existing.Values) removed.IsDeleted = true;
            var sales = saleService.CreateModels(snapshot.Sales, userId).ToList();
            var previousSales = (await saleRepository.ListAsync(userId, cancellationToken)).ToDictionary(item => item.Id);
            foreach (var sale in sales)
            {
                var production = saved[sale.ProductionId];
                validationService.Require(production.Quantity > 0, "Salve a produção antes de registrar vendas.");
                sale.RecipeName = production.RecipeName;
                sale.ProductionDate = production.Date;
                var packages = decimal.Floor((production.ActualYield - production.Losses) / production.PackageQty);
                sale.CostPerPackage = previousSales.TryGetValue(sale.Id, out var previousSale) && previousSale.ProductionId == sale.ProductionId
                    ? previousSale.CostPerPackage : packages > 0
                    ? decimal.Round((production.TotalCost ?? production.IngredientCostPerBatch * production.Quantity) / packages, 2)
                    : 0;
            }
            await workspaceRepository.SyncCatalogAsync(userId,
                marketService.CreateModels(snapshot.Market, userId),
                recipeService.CreateModels(snapshot.Recipes, userId),
                ingredientService.CreateModels(snapshot.Recipes, userId), sales, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            response = await ReadSnapshotAsync(cancellationToken);
        }, cancellationToken);
        return response!;
    }

    private async Task PriceProductionAsync(Models.Production production, DataSnapshotDto snapshot, CancellationToken ct)
    {
        var manualCost = production.IsStandalone ? production.TotalCost : null;
        production.IngredientCostPerBatch = 0;
        production.TotalCost = null;
        if (production.Quantity == 0) return;
        var recipe = snapshot.Recipes.Find(item => item.Id == production.RecipeId)
            ?? throw new ServiceException(ServiceError.InvalidData, "A produção precisa de uma receita cadastrada.");
        if (production.IsStandalone)
        {
            production.RecipeName = recipe.Name;
            production.YieldPerBatch = recipe.Yield;
            ApplyManualCost(production, manualCost!.Value);
            return;
        }
        validationService.Require(recipe.Ingredients.Any(item => item.Quantity > 0),
            "A receita precisa de pelo menos um insumo com quantidade positiva.");
        await stockService.ConsumeAsync(production, recipe, ct);
    }

    private static void ApplyManualCost(Models.Production production, decimal totalCost)
    {
        production.TotalCost = totalCost;
        production.IngredientCostPerBatch = decimal.Round(totalCost / production.Quantity, 8);
    }

    public void Validate(DataSnapshotDto snapshot)
    {
        validationService.Require(snapshot.Market is not null && snapshot.Recipes is not null &&
            snapshot.Productions is not null && snapshot.Sales is not null,
            "As listas market, recipes, productions e sales sao obrigatorias.");
        validationService.Require(snapshot.Market!.All(item => item is not null) &&
            snapshot.Recipes!.All(item => item is not null) && snapshot.Productions!.All(item => item is not null) &&
            snapshot.Sales!.All(item => item is not null), "As listas nao podem conter itens nulos.");
        marketService.Validate(snapshot.Market!);
        recipeService.Validate(snapshot.Recipes!);
        ingredientService.Validate(snapshot.Recipes!);
        productionService.Validate(snapshot.Productions!);
        saleService.Validate(snapshot.Sales!);
        ingredientService.ValidateReferences(snapshot.Recipes!, snapshot.Market!);
        saleService.ValidateReferences(snapshot.Sales!, snapshot.Productions!);
    }

    private async Task EnsureOwnershipAsync(DataSnapshotDto snapshot, string userId, CancellationToken cancellationToken)
    {
        if (await marketRepository.HasForeignIdsAsync(userId, snapshot.Market.Select(item => item.Id).ToArray(), cancellationToken) ||
            await recipeRepository.HasForeignIdsAsync(userId, snapshot.Recipes.Select(item => item.Id).ToArray(), cancellationToken) ||
            await ingredientRepository.HasForeignIdsAsync(userId, snapshot.Recipes.SelectMany(item => item.Ingredients).Select(item => item.Id).ToArray(), cancellationToken) ||
            await productionRepository.HasForeignIdsAsync(userId, snapshot.Productions.Select(item => item.Id).ToArray(), cancellationToken) ||
            await saleRepository.HasForeignIdsAsync(userId, snapshot.Sales.Select(item => item.Id).ToArray(), cancellationToken))
        {
            throw new ServiceException(ServiceError.Conflict, "Um ou mais identificadores ja pertencem a outra conta.");
        }
    }

    public Task ClaimLegacyAsync(string userId, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async () =>
        {
            await marketRepository.ClaimLegacyAsync(userId, cancellationToken);
            await recipeRepository.ClaimLegacyAsync(userId, cancellationToken);
            await productionRepository.ClaimLegacyAsync(userId, cancellationToken);
            await saleRepository.ClaimLegacyAsync(userId, cancellationToken);
            await ingredientRepository.ClaimLegacyAsync(userId, cancellationToken);
        }, cancellationToken);
}
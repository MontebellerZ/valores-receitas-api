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
        DataSnapshotDto? response = null;
        await unitOfWork.ExecuteAsync(async () =>
        {
            response = await ReplaceWithinTransactionAsync(snapshot, cancellationToken);
        }, cancellationToken);
        return response!;
    }

    public async Task<DataSnapshotDto> PatchAsync(WorkspacePatchDto patch, CancellationToken cancellationToken)
    {
        validationService.Require(patch.Market is not null && patch.Recipes is not null &&
            patch.Productions is not null && patch.Sales is not null && patch.Deleted is not null,
            "Informe as alterações e exclusões.");
        var deleted = patch.Deleted!;
        validationService.Require(deleted.Market is not null && deleted.Recipes is not null &&
            deleted.Productions is not null && deleted.Sales is not null, "Informe as listas de exclusões.");
        validationService.Require(patch.PartialRecipeIngredients || patch.DeletedIngredients is null or { Count: 0 },
            "Exclusões de ingredientes exigem o modo de edição parcial.");
        DataSnapshotDto? response = null;
        if (IsEmptyPatch(patch, deleted))
        {
            await unitOfWork.ExecuteAsync(async () =>
            {
                await workspaceRepository.AdvanceRevisionAsync(currentUser.Id, patch.Revision, cancellationToken);
                response = new([], [], [], [], await workspaceRepository.RevisionAsync(currentUser.Id, cancellationToken));
            }, cancellationToken);
            return response!;
        }
        if (IsMarketOnlyPatch(patch, deleted))
        {
            var changedMarket = patch.Market!;
            validationService.Require(changedMarket.All(item => item is not null),
                "As alterações não podem conter itens nulos.");
            marketService.Validate(changedMarket);
            await unitOfWork.ExecuteAsync(async () =>
            {
                await workspaceRepository.AdvanceRevisionAsync(currentUser.Id, patch.Revision, cancellationToken);
                await workspaceRepository.PatchMarketAsync(currentUser.Id,
                    marketService.CreateModels(changedMarket, currentUser.Id).ToList(), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                var market = await marketRepository.ListByIdsAsync(currentUser.Id,
                    changedMarket.Select(item => item.Id).ToArray(), cancellationToken);
                response = new(market, [], [], [], await workspaceRepository.RevisionAsync(currentUser.Id, cancellationToken));
            }, cancellationToken);
            return response!;
        }
        if (IsRecipeOnlyPatch(patch, deleted))
        {
            var changedRecipes = patch.Recipes!;
            validationService.Require(changedRecipes.All(recipe => recipe is not null),
                "As alterações não podem conter itens nulos.");
            recipeService.Validate(changedRecipes);
            ingredientService.Validate(changedRecipes);
            await unitOfWork.ExecuteAsync(async () =>
            {
                var recipeIds = changedRecipes.Select(recipe => recipe.Id).ToArray();
                var currentRecipes = await recipeRepository.ListByIdsAsync(currentUser.Id, recipeIds, cancellationToken);
                var effectiveRecipes = changedRecipes;
                if (patch.PartialRecipeIngredients)
                {
                    var deletedIngredients = patch.DeletedIngredients ?? [];
                    validationService.ValidateIds(deletedIngredients);
                    var recipeIdsSet = recipeIds.ToHashSet(StringComparer.Ordinal);
                    var editableIngredients = currentRecipes.Where(recipe => recipeIdsSet.Contains(recipe.Id))
                        .SelectMany(recipe => recipe.Ingredients).Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
                    validationService.Require(deletedIngredients.All(editableIngredients.Contains),
                        "Um ingrediente excluído não pertence às receitas alteradas.");
                    validationService.Require(!changedRecipes.SelectMany(recipe => recipe.Ingredients)
                        .Any(item => deletedIngredients.Contains(item.Id)),
                        "Um ingrediente não pode ser alterado e excluído na mesma requisição.");
                    effectiveRecipes = changedRecipes.Select(recipe =>
                    {
                        var existing = currentRecipes.Find(item => item.Id == recipe.Id)?.Ingredients ?? [];
                        var removed = deletedIngredients.Where(id => existing.Any(item => item.Id == id)).ToList();
                        return recipe with { Ingredients = Merge(existing, recipe.Ingredients, removed, item => item.Id) };
                    }).ToList();
                }

                recipeService.Validate(effectiveRecipes);
                ingredientService.Validate(effectiveRecipes);
                var ingredientIds = effectiveRecipes.SelectMany(recipe => recipe.Ingredients)
                    .Select(item => item.Id).ToArray();
                if (await recipeRepository.HasForeignIdsAsync(currentUser.Id, recipeIds, cancellationToken) ||
                    await ingredientRepository.HasForeignIdsAsync(currentUser.Id, ingredientIds, cancellationToken))
                    throw new ServiceException(ServiceError.Conflict, "Um ou mais identificadores ja pertencem a outra conta.");
                validationService.Require(!await ingredientRepository.HasOwnedIdsOutsideRecipesAsync(
                    currentUser.Id, ingredientIds, recipeIds, cancellationToken),
                    "Um ingrediente já pertence a outra receita não alterada.");

                var marketIds = effectiveRecipes.SelectMany(recipe => recipe.Ingredients)
                    .Select(item => item.MarketItemId).Distinct(StringComparer.Ordinal).ToArray();
                var market = await marketRepository.ListByIdsAsync(currentUser.Id, marketIds, cancellationToken);
                ingredientService.ValidateReferences(effectiveRecipes, market);

                await workspaceRepository.AdvanceRevisionAsync(currentUser.Id, patch.Revision, cancellationToken);
                await workspaceRepository.PatchRecipesAsync(currentUser.Id,
                    recipeService.CreateModels(effectiveRecipes, currentUser.Id).ToList(),
                    ingredientService.CreateModels(effectiveRecipes, currentUser.Id).ToList(), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                var savedRecipes = await recipeRepository.ListByIdsAsync(currentUser.Id, recipeIds, cancellationToken);
                if (patch.PartialRecipeIngredients)
                {
                    savedRecipes = savedRecipes.Select(recipe =>
                    {
                        var changed = changedRecipes.Find(item => item.Id == recipe.Id)!;
                        var changedIngredientIds = changed.Ingredients.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
                        return recipe with
                        {
                            IngredientCount = recipe.Ingredients.Count,
                            Ingredients = recipe.Ingredients.Where(item => changedIngredientIds.Contains(item.Id)).ToList(),
                        };
                    }).ToList();
                }
                response = new([], savedRecipes, [], [],
                    await workspaceRepository.RevisionAsync(currentUser.Id, cancellationToken));
            }, cancellationToken);
            return response!;
        }
        if (IsProductionSalesOnlyPatch(patch, deleted))
            return patch.Productions!.Count == 0
                ? await PatchSalesOnlyAsync(patch, cancellationToken)
                : await PatchProductionSalesAsync(patch, cancellationToken);

        await unitOfWork.ExecuteAsync(async () =>
        {
            var current = await ReadSnapshotAsync(cancellationToken);
            var recipes = patch.Recipes!;
            if (patch.PartialRecipeIngredients)
            {
                ingredientService.Validate(recipes);
                var deletedIngredients = patch.DeletedIngredients ?? [];
                validationService.ValidateIds(deletedIngredients);
                var recipeIds = recipes.Select(recipe => recipe.Id).ToHashSet(StringComparer.Ordinal);
                var editableIngredients = current.Recipes.Where(recipe => recipeIds.Contains(recipe.Id))
                    .SelectMany(recipe => recipe.Ingredients).Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
                validationService.Require(deletedIngredients.All(editableIngredients.Contains),
                    "Um ingrediente excluído não pertence às receitas alteradas.");
                validationService.Require(!recipes.SelectMany(recipe => recipe.Ingredients)
                    .Any(item => deletedIngredients.Contains(item.Id)),
                    "Um ingrediente não pode ser alterado e excluído na mesma requisição.");
                recipes = recipes.Select(recipe =>
                {
                    var existing = current.Recipes.Find(item => item.Id == recipe.Id)?.Ingredients ?? [];
                    var removedIngredients = deletedIngredients.Where(id => existing.Any(item => item.Id == id)).ToList();
                    return recipe with { Ingredients = Merge(existing, recipe.Ingredients, removedIngredients, item => item.Id) };
                }).ToList();
            }
            var merged = new DataSnapshotDto(
                Merge(current.Market, patch.Market!, deleted.Market!, item => item.Id),
                Merge(current.Recipes, recipes, deleted.Recipes!, item => item.Id),
                Merge(current.Productions, patch.Productions!, deleted.Productions!, item => item.Id),
                Merge(current.Sales, patch.Sales!, deleted.Sales!, item => item.Id), patch.Revision);
            ingredientService.Validate(merged.Recipes);
            // Removing a catalog item also removes its references, as in the catalog editor.
            if (deleted.Market!.Count > 0)
                merged = merged with
                {
                    Recipes = merged.Recipes.Select(recipe => recipe with
                    {
                        Ingredients = recipe.Ingredients.Where(item => !deleted.Market.Contains(item.MarketItemId)).ToList(),
                    }).ToList()
                };
            Validate(merged);
            var saved = await ReplaceWithinTransactionAsync(merged, cancellationToken);
            response = new(
                saved.Market.Where(item => patch.Market!.Any(changed => changed.Id == item.Id)).ToList(),
                saved.Recipes.Where(item => patch.Recipes!.Any(changed => changed.Id == item.Id))
                    .Select(item => patch.PartialRecipeIngredients
                        ? item with
                        {
                            IngredientCount = item.Ingredients.Count,
                            Ingredients = item.Ingredients.Where(ingredient =>
                                patch.Recipes!.Any(recipe => recipe.Id == item.Id &&
                                    recipe.Ingredients.Any(changed => changed.Id == ingredient.Id))).ToList(),
                        }
                        : item).ToList(),
                saved.Productions.Where(item => patch.Productions!.Any(changed => changed.Id == item.Id)).ToList(),
                saved.Sales.Where(item => patch.Sales!.Any(changed => changed.Id == item.Id)).ToList(),
                saved.Revision);
        }, cancellationToken);
        return response!;
    }

    private static bool IsEmptyPatch(WorkspacePatchDto patch, WorkspaceDeletionsDto deleted) =>
        patch.Market!.Count == 0 && patch.Recipes!.Count == 0 && patch.Productions!.Count == 0 && patch.Sales!.Count == 0 &&
        deleted.Market!.Count == 0 && deleted.Recipes!.Count == 0 && deleted.Productions!.Count == 0 && deleted.Sales!.Count == 0 &&
        !patch.PartialRecipeIngredients && (patch.DeletedIngredients is null or { Count: 0 });

    private static bool IsMarketOnlyPatch(WorkspacePatchDto patch, WorkspaceDeletionsDto deleted) =>
        patch.Market!.Count > 0 && patch.Recipes!.Count == 0 && patch.Productions!.Count == 0 && patch.Sales!.Count == 0 &&
        deleted.Market!.Count == 0 && deleted.Recipes!.Count == 0 && deleted.Productions!.Count == 0 && deleted.Sales!.Count == 0 &&
        !patch.PartialRecipeIngredients && (patch.DeletedIngredients is null or { Count: 0 });

    private static bool IsRecipeOnlyPatch(WorkspacePatchDto patch, WorkspaceDeletionsDto deleted) =>
        patch.Market!.Count == 0 && patch.Recipes!.Count > 0 && patch.Productions!.Count == 0 && patch.Sales!.Count == 0 &&
        deleted.Market!.Count == 0 && deleted.Recipes!.Count == 0 && deleted.Productions!.Count == 0 && deleted.Sales!.Count == 0;

    private static bool IsProductionSalesOnlyPatch(WorkspacePatchDto patch, WorkspaceDeletionsDto deleted) =>
        patch.Market!.Count == 0 && patch.Recipes!.Count == 0 &&
        (patch.Productions!.Count > 0 || patch.Sales!.Count > 0) &&
        deleted.Market!.Count == 0 && deleted.Recipes!.Count == 0 && deleted.Productions!.Count == 0 && deleted.Sales!.Count == 0 &&
        !patch.PartialRecipeIngredients && (patch.DeletedIngredients is null or { Count: 0 });

    private async Task<DataSnapshotDto> PatchSalesOnlyAsync(WorkspacePatchDto patch, CancellationToken ct)
    {
        var changedSales = patch.Sales!;
        validationService.Require(changedSales.All(item => item is not null),
            "As alterações não podem conter itens nulos.");
        saleService.Validate(changedSales);
        DataSnapshotDto? response = null;
        await unitOfWork.ExecuteAsync(async () =>
        {
            var userId = currentUser.Id;
            await workspaceRepository.AdvanceRevisionAsync(userId, patch.Revision, ct);
            var saleIds = changedSales.Select(item => item.Id).ToArray();
            if (await saleRepository.HasForeignIdsAsync(userId, saleIds, ct))
                throw new ServiceException(ServiceError.Conflict, "Um ou mais identificadores ja pertencem a outra conta.");

            var previousSales = await workspaceRepository.SalesByIdsAsync(userId, saleIds, ct);
            var previousSaleValues = previousSales.ToDictionary(item => item.Id,
                item => (item.ProductionId, item.CostPerPackage, item.IsDeleted), StringComparer.Ordinal);
            var productionIds = changedSales.Select(item => item.ProductionId).Distinct(StringComparer.Ordinal).ToArray();
            var productionEntities = await workspaceRepository.ProductionsByIdsAsync(userId, productionIds, ct);
            var productionById = productionEntities.ToDictionary(item => item.Id, StringComparer.Ordinal);
            var productionDtos = productionEntities.Where(item => !item.IsDeleted).Select(ToDto).ToList();
            var existingPackageTotals = await workspaceRepository.SalePackageTotalsAsync(userId, productionIds, saleIds, ct);
            saleService.ValidateReferences(changedSales, productionDtos, existingPackageTotals);

            var saleModels = saleService.CreateModels(changedSales, userId).ToList();
            foreach (var model in saleModels)
            {
                var production = productionById.GetValueOrDefault(model.ProductionId);
                validationService.Require(production is not null && !production.IsDeleted && production.Quantity > 0,
                    "Salve a produção antes de registrar vendas.");
                var packages = decimal.Floor((production!.ActualYield - production.Losses) / production.PackageQty);
                model.RecipeName = production.RecipeName;
                model.ProductionDate = production.Date;
                model.CostPerPackage = previousSaleValues.TryGetValue(model.Id, out var previous) &&
                    !previous.IsDeleted && previous.ProductionId == model.ProductionId
                    ? previous.CostPerPackage
                    : packages > 0
                        ? decimal.Round((production.TotalCost ?? production.IngredientCostPerBatch * production.Quantity) / packages, 2)
                        : 0;
            }

            foreach (var model in saleModels)
            {
                var previous = previousSales.Find(item => item.Id == model.Id);
                if (previous is null)
                    workspaceRepository.AddSale(model);
                else
                    UpdateSale(previous, model);
            }
            await unitOfWork.SaveChangesAsync(ct);
            var savedSales = await saleRepository.ListByIdsAsync(userId, saleIds, ct);
            response = new([], [], [], savedSales, await workspaceRepository.RevisionAsync(userId, ct));
        }, ct);
        return response!;
    }

    private async Task<DataSnapshotDto> PatchProductionSalesAsync(WorkspacePatchDto patch, CancellationToken ct)
    {
        var changedProductions = patch.Productions!;
        var changedSales = patch.Sales!;
        validationService.Require(changedProductions.All(item => item is not null) && changedSales.All(item => item is not null),
            "As alterações não podem conter itens nulos.");
        productionService.Validate(changedProductions);
        saleService.Validate(changedSales);
        DataSnapshotDto? response = null;
        await unitOfWork.ExecuteAsync(async () =>
        {
            var userId = currentUser.Id;
            await workspaceRepository.AdvanceRevisionAsync(userId, patch.Revision, ct);
            var productionIds = changedProductions.Select(item => item.Id).ToArray();
            var saleIds = changedSales.Select(item => item.Id).ToArray();
            if (await productionRepository.HasForeignIdsAsync(userId, productionIds, ct) ||
                await saleRepository.HasForeignIdsAsync(userId, saleIds, ct))
                throw new ServiceException(ServiceError.Conflict, "Um ou mais identificadores ja pertencem a outra conta.");

            var previousSales = await workspaceRepository.SalesByIdsAsync(userId, saleIds, ct);
            var previousSaleValues = previousSales.ToDictionary(item => item.Id,
                item => (item.ProductionId, item.CostPerPackage, item.IsDeleted), StringComparer.Ordinal);
            var changedSaleIds = saleIds.ToHashSet(StringComparer.Ordinal);
            var affectedProductionIds = productionIds
                .Concat(changedSales.Select(item => item.ProductionId))
                .Concat(previousSales.Select(item => item.ProductionId))
                .Distinct(StringComparer.Ordinal).ToArray();
            var productionEntities = (await workspaceRepository.ProductionsByIdsAsync(userId, affectedProductionIds, ct))
                .ToDictionary(item => item.Id, StringComparer.Ordinal);

            var scopedSales = await workspaceRepository.SalesByProductionIdsAsync(userId, affectedProductionIds, ct);
            var salesById = scopedSales.Concat(previousSales).DistinctBy(item => item.Id)
                .ToDictionary(item => item.Id, StringComparer.Ordinal);
            var effectiveSales = salesById.Values.Where(item => !item.IsDeleted && !changedSaleIds.Contains(item.Id)).ToList();
            var saleModels = saleService.CreateModels(changedSales, userId).ToList();
            foreach (var model in saleModels)
            {
                if (salesById.TryGetValue(model.Id, out var previous))
                {
                    UpdateSale(previous, model);
                    effectiveSales.Add(previous);
                }
                else
                {
                    workspaceRepository.AddSale(model);
                    salesById.Add(model.Id, model);
                    effectiveSales.Add(model);
                }
            }

            var pricingRecipeIds = changedProductions.Where(model =>
                    !productionEntities.TryGetValue(model.Id, out var previous) || previous.Quantity == 0)
                .Where(model => model.Quantity > 0)
                .Select(model => model.RecipeId).Distinct(StringComparer.Ordinal).ToArray();
            List<RecipeDto> pricingRecipes = pricingRecipeIds.Length == 0
                ? []
                : await recipeRepository.ListByIdsAsync(userId, pricingRecipeIds, ct);
            var pricingSnapshot = new DataSnapshotDto([], pricingRecipes, [], [], patch.Revision);
            foreach (var model in productionService.CreateModels(changedProductions, userId))
            {
                if (productionEntities.TryGetValue(model.Id, out var previous))
                {
                    validationService.Require(!previous.IsDeleted, "Uma produção removida não pode ser reutilizada.");
                    if (previous.Quantity > 0)
                    {
                        validationService.Require(previous.RecipeId == model.RecipeId && previous.Quantity == model.Quantity,
                            "Receita e quantidade de uma produção salva são imutáveis. Registre uma nova produção.");
                        validationService.Require(previous.IsStandalone == model.IsStandalone,
                            "O tipo de uma produção salva (normal ou avulsa) não pode ser alterado.");
                        validationService.Require(previous.PackageQty == model.PackageQty ||
                            !effectiveSales.Any(sale => sale.ProductionId == model.Id),
                            "Uma produção com vendas não pode mudar a quantidade por pacote.");
                        previous.Date = model.Date;
                        previous.ActualYield = model.ActualYield;
                        previous.Losses = model.Losses;
                        previous.Margin = model.Margin;
                        previous.PackageQty = model.PackageQty;
                        previous.ExpectedSalePrice = model.ExpectedSalePrice;
                        previous.LostPackages = model.LostPackages;
                        if (previous.IsStandalone) ApplyManualCost(previous, model.TotalCost!.Value);
                    }
                    else
                    {
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
                        await PriceProductionAsync(previous, pricingSnapshot, ct);
                    }
                }
                else
                {
                    await PriceProductionAsync(model, pricingSnapshot, ct);
                    workspaceRepository.AddProduction(model);
                    productionEntities.Add(model.Id, model);
                }
            }

            foreach (var sale in effectiveSales)
            {
                if (productionEntities.TryGetValue(sale.ProductionId, out var production) && !production.IsDeleted)
                {
                    sale.RecipeName = production.RecipeName;
                    sale.ProductionDate = production.Date;
                }
            }
            foreach (var saleDto in changedSales)
            {
                var sale = effectiveSales.Single(item => item.Id == saleDto.Id);
                var production = productionEntities.GetValueOrDefault(sale.ProductionId);
                validationService.Require(production is not null && !production.IsDeleted && production.Quantity > 0,
                    "Salve a produção antes de registrar vendas.");
                var packages = decimal.Floor((production!.ActualYield - production.Losses) / production.PackageQty);
                sale.CostPerPackage = previousSaleValues.TryGetValue(sale.Id, out var oldSale) &&
                    !oldSale.IsDeleted && oldSale.ProductionId == sale.ProductionId
                    ? oldSale.CostPerPackage
                    : packages > 0
                        ? decimal.Round((production.TotalCost ?? production.IngredientCostPerBatch * production.Quantity) / packages, 2)
                        : 0;
            }

            var effectiveProductionDtos = productionEntities.Values.Where(item => !item.IsDeleted)
                .Select(ToDto).ToList();
            var effectiveSaleDtos = effectiveSales.Select(ToDto).ToList();
            saleService.Validate(effectiveSaleDtos);
            saleService.ValidateReferences(effectiveSaleDtos, effectiveProductionDtos);

            await unitOfWork.SaveChangesAsync(ct);
            var savedProductions = await productionRepository.ListByIdsAsync(userId, productionIds, ct);
            var savedSales = await saleRepository.ListByIdsAsync(userId, saleIds, ct);
            response = new([], [], savedProductions, savedSales, await workspaceRepository.RevisionAsync(userId, ct));
        }, ct);
        return response!;
    }

    private static ProductionDto ToDto(Models.Production item) =>
        new(item.Id, item.Date, item.RecipeId, item.RecipeName, item.Quantity, item.ActualYield,
            item.Losses, item.Margin, item.PackageQty, item.YieldPerBatch, item.IngredientCostPerBatch,
            item.ExpectedSalePrice, item.TotalCost, item.IsStandalone, item.LostPackages);

    private static SaleDto ToDto(Models.Sale item) =>
        new(item.Id, item.ProductionId, item.ProductionDate, item.RecipeName, item.Date,
            item.Packages, item.PricePerPackage, item.CostPerPackage, item.Customer,
            item.Paid, item.Notes, item.Donated);

    private static void UpdateSale(Models.Sale target, Models.Sale source)
    {
        target.ProductionId = source.ProductionId;
        target.ProductionDate = source.ProductionDate;
        target.RecipeName = source.RecipeName;
        target.Date = source.Date;
        target.Packages = source.Packages;
        target.PricePerPackage = source.PricePerPackage;
        target.CostPerPackage = source.CostPerPackage;
        target.Customer = source.Customer;
        target.Paid = source.Paid;
        target.Donated = source.Donated;
        target.Notes = source.Notes;
        target.UserId = source.UserId;
        target.IsDeleted = false;
    }

    private List<T> Merge<T>(List<T> current, List<T> changed, List<string> deleted, Func<T, string> id)
        where T : class
    {
        validationService.Require(changed.All(item => item is not null), "As alterações não podem conter itens nulos.");
        validationService.ValidateIds(changed.Select(id));
        validationService.ValidateIds(deleted);
        validationService.Require(!changed.Any(item => deleted.Contains(id(item))),
            "Um registro não pode ser alterado e excluído na mesma requisição.");
        validationService.Require(deleted.All(value => current.Any(item => id(item) == value)),
            "Um registro excluído não foi encontrado nesta conta.");
        var changedIds = changed.Select(id).ToHashSet(StringComparer.Ordinal);
        var deletedIds = deleted.ToHashSet(StringComparer.Ordinal);
        return current.Where(item => !changedIds.Contains(id(item)) && !deletedIds.Contains(id(item)))
            .Concat(changed).ToList();
    }

    private async Task<DataSnapshotDto> ReplaceWithinTransactionAsync(DataSnapshotDto snapshot, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
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
        return await ReadSnapshotAsync(cancellationToken);
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
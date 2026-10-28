using ValoresReceitas.Api.Services.Interfaces;
using ValoresReceitas.Api.Repositories.Interfaces;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;
using System.Text.Json;

namespace ValoresReceitas.Api.Services;

public sealed class StockService(IStockRepository repository, IWorkspaceRepository workspace,
    ICurrentUserService currentUser, IUnitOfWork unitOfWork,
    IDataValidationService validationService) : IStockService
{
    public Task<List<StockLotDto>> ListAsync(CancellationToken ct, bool includeInactive = false) =>
        repository.ListAsync(currentUser.Id, ct, includeInactive);
    public async Task<StockLotDto?> FindAsync(string id, CancellationToken ct)
    {
        var lot = await repository.FindAsync(currentUser.Id, id, ct);
        return lot is null ? null : repository.ToDto(lot);
    }

    public Task<List<StockConsumptionDto>> ConsumptionsAsync(string productionId, CancellationToken ct) =>
        repository.ConsumptionsAsync(currentUser.Id, productionId, ct);

    public async Task<StockMutationResponseDto> CreateAsync(CreateStockLotRequestDto request, CancellationToken ct)
    {
        validationService.Require(request.EntryDate != default && request.PackageQty > 0 &&
            request.PackagePrice >= 0 && decimal.Round(request.PackageQty, 4) == request.PackageQty &&
            decimal.Round(request.PackagePrice, 2) == request.PackagePrice, "Informe data, quantidade (até 4 casas) e preço (até 2 casas) válidos.");
        validationService.Require(request.PackageCount > 0, "Informe uma quantidade inteira e positiva de pacotes.");
        var lots = new List<StockLot>();
        await unitOfWork.ExecuteAsync(async () =>
        {
            await workspace.AdvanceRevisionAsync(currentUser.Id, request.Revision, ct);
            var item = await repository.ItemAsync(currentUser.Id, request.MarketItemId, ct)
                ?? throw new ServiceException(ServiceError.InvalidData, "Insumo não encontrado.");
            validationService.Require(!string.IsNullOrWhiteSpace(item.Name) && !string.IsNullOrWhiteSpace(item.Unit),
                "Preencha nome e unidade do insumo antes de registrar uma compra.");
            for (var index = 0; index < request.PackageCount; index++)
            {
                ct.ThrowIfCancellationRequested();
                var lot = new StockLot
                {
                    Id = Guid.NewGuid().ToString(), UserId = currentUser.Id, MarketItemId = item.Id,
                    ItemName = item.Name, Unit = item.Unit, EntryDate = request.EntryDate,
                    EntryOrder = request.Revision + 1, PackageQty = request.PackageQty,
                    PackagePrice = request.PackagePrice, RemainingQty = request.PackageQty, IsActive = true,
                };
                repository.Add(lot);
                lots.Add(lot);
            }
        }, ct);
        var created = lots.Select(repository.ToDto).ToList();
        return new(created[0], request.Revision + 1, created);
    }

    public async Task<StockMutationResponseDto> AdjustAsync(string id, AdjustStockLotRequestDto request, CancellationToken ct)
    {
        StockLot? lot = null;
        await unitOfWork.ExecuteAsync(async () =>
        {
            await workspace.AdvanceRevisionAsync(currentUser.Id, request.Revision, ct);
            lot = await repository.FindAsync(currentUser.Id, id, ct)
                ?? throw new ServiceException(ServiceError.InvalidData, "Lote não encontrado.");
            validationService.Require(lot.IsActive && request.RemainingQty >= 0 &&
                request.RemainingQty < lot.RemainingQty && decimal.Round(request.RemainingQty, 4) == request.RemainingQty &&
                !string.IsNullOrWhiteSpace(request.Reason), "A baixa deve diminuir o saldo de um lote ativo e informar o motivo.");
            repository.Add(new StockAdjustment
            {
                Id = Guid.NewGuid().ToString(), StockLotId = lot.Id, CreatedAt = DateTimeOffset.UtcNow,
                PreviousQty = lot.RemainingQty, RemainingQty = request.RemainingQty, Reason = request.Reason.Trim(),
            });
            lot.RemainingQty = request.RemainingQty;
            lot.IsActive = lot.RemainingQty > 0;
        }, ct);
        return new(repository.ToDto(lot!), request.Revision + 1);
    }

    public async Task DeleteAsync(string id, long revision, CancellationToken ct)
    {
        await unitOfWork.ExecuteAsync(async () =>
        {
            await workspace.AdvanceRevisionAsync(currentUser.Id, revision, ct);
            var lot = await repository.FindAsync(currentUser.Id, id, ct)
                ?? throw new ServiceException(ServiceError.NotFound, "Pacote não encontrado.");
            if (await repository.HasProductionUsageAsync(lot.Id, ct))
                throw new ServiceException(ServiceError.Conflict,
                    "Este pacote foi utilizado em uma produção e não pode ser excluído.");
            repository.Remove(lot);
        }, ct);
    }

    public async Task<StockMutationResponseDto> UpdateAsync(UpdateStockLotsRequestDto request, CancellationToken ct)
    {
        validationService.Require(request.LotIds is { Count: > 0 and <= 1000 } &&
            request.LotIds.All(id => !string.IsNullOrWhiteSpace(id) && id.Length <= 64) &&
            request.LotIds.Distinct().Count() == request.LotIds.Count,
            "Selecione de 1 a 1.000 pacotes diferentes.");
        validationService.Require(!string.IsNullOrWhiteSpace(request.Reason) && request.Reason.Length <= 500,
            "Informe o motivo da correção (até 500 caracteres).");
        validationService.Require(request.MarketItemId is not null || request.EntryDate.HasValue ||
            request.PackageQty.HasValue || request.PackagePrice.HasValue, "Informe pelo menos um campo para corrigir.");
        validationService.Require(!request.EntryDate.HasValue || request.EntryDate.Value != default,
            "Informe uma data de compra válida.");
        validationService.Require(!request.PackageQty.HasValue || request.PackageQty > 0 &&
            request.PackageQty <= 99999999999999m && decimal.Round(request.PackageQty.Value, 4) == request.PackageQty,
            "Informe uma quantidade positiva com até 4 casas decimais.");
        validationService.Require(!request.PackagePrice.HasValue || request.PackagePrice >= 0 &&
            request.PackagePrice <= 9999999999999999m && decimal.Round(request.PackagePrice.Value, 2) == request.PackagePrice,
            "Informe um preço não negativo com até 2 casas decimais.");
        var corrected = new List<StockLotDto>();
        await unitOfWork.ExecuteAsync(async () =>
        {
            await workspace.AdvanceRevisionAsync(currentUser.Id, request.Revision, ct);
            var lots = await repository.SelectedAsync(currentUser.Id, request.LotIds, ct);
            validationService.Require(lots.Count == request.LotIds.Count,
                "Um ou mais pacotes não foram encontrados. Recarregue o estoque.");
            if (lots.Count > 1)
            {
                validationService.Require(lots.All(lot => lot.EntryOrder == lots[0].EntryOrder &&
                    lot.MarketItemId == lots[0].MarketItemId),
                    "A edição conjunta permite apenas pacotes do mesmo insumo cadastrados na mesma compra.");
                validationService.Require(lots.Count == await repository.PurchaseCountAsync(currentUser.Id, lots[0].EntryOrder, ct),
                    "A edição conjunta deve incluir todos os pacotes da compra, inclusive os esgotados.");
            }
            MarketItem? item = null;
            if (request.MarketItemId is not null)
            {
                item = await repository.ItemAsync(currentUser.Id, request.MarketItemId, ct)
                    ?? throw new ServiceException(ServiceError.InvalidData, "Insumo não encontrado.");
                validationService.Require(!string.IsNullOrWhiteSpace(item.Name) && !string.IsNullOrWhiteSpace(item.Unit),
                    "Preencha nome e unidade do insumo antes de corrigir a compra.");
            }
            foreach (var lot in lots)
            {
                var before = repository.ToDto(lot);
                var withdrawn = lot.PackageQty - lot.RemainingQty;
                var quantity = request.PackageQty ?? lot.PackageQty;
                validationService.Require(quantity >= withdrawn,
                    $"O pacote {lot.Id} já teve saída de {withdrawn} {lot.Unit}. A quantidade comprada não pode ser menor.");
                if (item is not null && item.Id != lot.MarketItemId)
                {
                    validationService.Require(!await repository.HasConsumptionAsync(lot.Id, ct),
                        $"O pacote {lot.Id} já foi usado em uma produção. Não é possível trocar seu insumo.");
                    validationService.Require(withdrawn == 0 || item.Unit == lot.Unit,
                        $"O pacote {lot.Id} tem baixas registradas. Não é possível trocar para um insumo com outra unidade.");
                    lot.MarketItemId = item.Id;
                    lot.ItemName = item.Name;
                    lot.Unit = item.Unit;
                }
                if (request.EntryDate.HasValue && request.EntryDate != lot.EntryDate)
                {
                    var firstProduction = await repository.FirstProductionDateAsync(lot.Id, ct);
                    validationService.Require(!firstProduction.HasValue || request.EntryDate <= firstProduction,
                        $"A compra do pacote {lot.Id} não pode ser posterior à produção que o consumiu.");
                    lot.EntryDate = request.EntryDate.Value;
                }
                lot.PackageQty = quantity;
                lot.PackagePrice = request.PackagePrice ?? lot.PackagePrice;
                lot.RemainingQty = quantity - withdrawn;
                lot.IsActive = lot.RemainingQty > 0;
                var after = repository.ToDto(lot);
                if (before != after)
                {
                    repository.Add(new StockAdjustment
                    {
                        Id = Guid.NewGuid().ToString(), StockLotId = lot.Id, CreatedAt = DateTimeOffset.UtcNow,
                        PreviousQty = before.RemainingQty, RemainingQty = lot.RemainingQty, Reason = request.Reason.Trim(),
                        PreviousData = JsonSerializer.Serialize(before), CorrectedData = JsonSerializer.Serialize(after),
                    });
                }
                corrected.Add(after);
            }
        }, ct);
        return new(corrected[0], request.Revision + 1, corrected);
    }

    public async Task ConsumeAsync(Production production, RecipeDto recipe, CancellationToken ct)
    {
        decimal total = 0;
        foreach (var ingredient in recipe.Ingredients.Where(item => item.Quantity > 0))
        {
            var required = ingredient.Quantity * production.Quantity;
            validationService.Require(decimal.Round(required, 4) == required, "O consumo deve ter no máximo 4 casas decimais.");
            var lots = await repository.AvailableAsync(currentUser.Id, ingredient.MarketItemId, ct);
            if (lots.Sum(lot => lot.RemainingQty) < required)
            {
                var item = await repository.ItemAsync(currentUser.Id, ingredient.MarketItemId, ct);
                throw new ServiceException(ServiceError.Conflict, "Estoque insuficiente.",
                    $"O insumo '{item?.Name ?? ingredient.MarketItemId}' precisa de {required} e possui {lots.Sum(lot => lot.RemainingQty)}. Nenhuma baixa foi salva.");
            }
            foreach (var lot in lots)
            {
                var quantity = Math.Min(required, lot.RemainingQty);
                if (quantity == 0) continue;
                var cost = decimal.Round(quantity * lot.PackagePrice / lot.PackageQty, 8);
                repository.Add(new StockConsumption
                {
                    Id = Guid.NewGuid().ToString(), ProductionId = production.Id, StockLotId = lot.Id,
                    Quantity = quantity, Cost = cost,
                    EntryDate = lot.EntryDate, PackageQty = lot.PackageQty, PackagePrice = lot.PackagePrice,
                });
                lot.RemainingQty -= quantity;
                lot.IsActive = lot.RemainingQty > 0;
                total += cost;
                required -= quantity;
                if (required == 0) break;
            }
        }
        production.TotalCost = total;
        production.IngredientCostPerBatch = decimal.Round(total / production.Quantity, 8);
        production.RecipeName = recipe.Name;
        production.YieldPerBatch = recipe.Yield;
    }
}
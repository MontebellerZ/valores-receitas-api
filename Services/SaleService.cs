using ValoresReceitas.Api.Services.Interfaces;
using ValoresReceitas.Api.Repositories.Interfaces;
using ValoresReceitas.Api.DTOs;
using ValoresReceitas.Api.Models;

namespace ValoresReceitas.Api.Services;

public sealed class SaleService(ISaleRepository repository, ICurrentUserService currentUser, IDataValidationService validationService) : ISaleService
{
    public Task<List<SaleDto>> ListAsync(CancellationToken cancellationToken) =>
        repository.ListAsync(currentUser.Id, cancellationToken);

    public void Validate(IReadOnlyCollection<SaleDto> sales)
    {
        validationService.ValidateIds(sales.Select(item => item.Id));
        validationService.Require(sales.All(item => item.Packages > 0 && item.PricePerPackage >= 0 &&
            item.CostPerPackage >= 0), "Pacotes devem ser positivos e valores de venda nao podem ser negativos.");
        validationService.Require(sales.All(item => item.Date != default), "As datas de venda sao obrigatorias.");
        validationService.Require(sales.All(item => !item.Donated || (item.PricePerPackage == 0 && item.Paid)),
            "Vendas doadas devem ter preco zero e nao podem ficar a receber.");
    }

    public void ValidateReferences(IEnumerable<SaleDto> sales, IEnumerable<ProductionDto> productions)
    {
        var productionById = productions.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var soldPackages = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var sale in sales)
        {
            if (!productionById.TryGetValue(sale.ProductionId, out var production))
            {
                throw new ServiceException(ServiceError.InvalidData, "Dados invalidos",
                    $"A venda '{sale.Id}' referencia uma producao inexistente.");
            }
            validationService.Require(sale.Date >= production.Date,
                $"A venda '{sale.Id}' deve ocorrer na data da producao ou depois dela.");
            soldPackages[sale.ProductionId] = soldPackages.GetValueOrDefault(sale.ProductionId) + sale.Packages;
        }
        foreach (var (productionId, packages) in soldPackages)
        {
            var production = productionById[productionId];
            var available = production.PackageCount - production.LostPackages;
            validationService.Require(packages <= available,
                $"A producao '{production.RecipeName}' nao possui pacotes suficientes para as vendas informadas.");
        }
    }

    public IEnumerable<Sale> CreateModels(IEnumerable<SaleDto> sales, string userId) =>
        sales.Select(item => new Sale
        {
            Id = item.Id, UserId = userId, ProductionId = item.ProductionId, ProductionDate = item.ProductionDate,
            RecipeName = item.RecipeName, Date = item.Date, Packages = item.Packages,
            PricePerPackage = item.PricePerPackage, CostPerPackage = item.CostPerPackage,
            Customer = item.Customer, Paid = item.Paid, Donated = item.Donated, Notes = item.Notes,
        });
}
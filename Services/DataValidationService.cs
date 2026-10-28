using ValoresReceitas.Api.Services.Interfaces;

namespace ValoresReceitas.Api.Services;

public sealed class DataValidationService : IDataValidationService
{
    public void Require(bool condition, string detail)
    {
        if (!condition)
        {
            throw new ServiceException(ServiceError.InvalidData, "Dados invalidos", detail);
        }
    }

    public void ValidateIds(IEnumerable<string> ids)
    {
        var values = ids.ToArray();
        Require(values.All(id => !string.IsNullOrWhiteSpace(id) && id.Length <= 64) &&
            values.Distinct(StringComparer.Ordinal).Count() == values.Length,
            "Os identificadores devem ser unicos e nao podem estar vazios.");
    }
}
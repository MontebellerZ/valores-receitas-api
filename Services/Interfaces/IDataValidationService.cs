namespace ValoresReceitas.Api.Services.Interfaces;

public interface IDataValidationService
{
    void Require(bool condition, string detail);
    void ValidateIds(IEnumerable<string> ids);
}

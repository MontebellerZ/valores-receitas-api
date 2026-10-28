namespace ValoresReceitas.Api.Services;

public enum ServiceError
{
    InvalidData,
    Conflict,
    Unauthorized,
    Forbidden,
    Locked,
    Unavailable,
    NotFound,
}

public sealed class ServiceException(
    ServiceError error,
    string title,
    string? detail = null,
    string? code = null,
    IDictionary<string, string[]>? errors = null) : Exception(title)
{
    public ServiceError Error { get; } = error;
    public string? Detail { get; } = detail;
    public string? Code { get; } = code;
    public IDictionary<string, string[]>? Errors { get; } = errors;
}
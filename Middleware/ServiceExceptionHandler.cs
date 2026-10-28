using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ValoresReceitas.Api.Services;

namespace ValoresReceitas.Api.Middleware;

public sealed class ServiceExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ServiceExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ServiceException failure)
        {
            return false;
        }
        logger.LogWarning(exception, "Operação recusada: {Error}", failure.Error);
        var status = failure.Error switch
        {
            ServiceError.Conflict => StatusCodes.Status409Conflict,
            ServiceError.NotFound => StatusCodes.Status404NotFound,
            ServiceError.Unauthorized => StatusCodes.Status401Unauthorized,
            ServiceError.Forbidden => StatusCodes.Status403Forbidden,
            ServiceError.Locked => StatusCodes.Status423Locked,
            ServiceError.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest,
        };
        ProblemDetails problem = failure.Errors is null ? new ProblemDetails() : new ValidationProblemDetails(failure.Errors);
        problem.Title = failure.Message;
        problem.Detail = failure.Detail;
        problem.Status = status;
        if (failure.Code is not null)
        {
            problem.Extensions["code"] = failure.Code;
        }
        context.Response.StatusCode = status;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
        return true;
    }
}
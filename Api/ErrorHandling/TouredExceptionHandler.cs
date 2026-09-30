using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TourEd.Lib.Abstractions.Exceptions;

namespace Api.ErrorHandling;

/// <summary>
/// Translates TourEd's expected domain exceptions into problem responses.
/// Every other exception remains unhandled and results in a generic 500.
/// </summary>
public sealed class TouredExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;

    public TouredExceptionHandler(IProblemDetailsService problemDetailsService)
    {
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            RequestValidationException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = exception.Message
            },
            AccessDeniedException => new ProblemDetails { Status = StatusCodes.Status403Forbidden },
            EntityNotFoundException => new ProblemDetails { Status = StatusCodes.Status404NotFound },
            ConflictException => new ProblemDetails { Status = StatusCodes.Status409Conflict },
            _ => null
        };
        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        // The status code is the contract; a problem body is only added when the client accepts it.
        await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem
        });
        return true;
    }
}

using CEMS.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = new ProblemDetails
        {
            Instance = httpContext.Request.Path
        };

        switch (exception)
        {
            case ValidationException validationException:
                problem.Status = StatusCodes.Status400BadRequest;
                problem.Title = "Validation failed";
                problem.Extensions["errors"] = validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                break;

            case BadRequestException badRequestException:
                problem.Status = StatusCodes.Status400BadRequest;
                problem.Title = "Bad request";
                problem.Extensions["errors"] = badRequestException.Errors;
                break;

            case AuthenticationException authenticationException:
                problem.Status = StatusCodes.Status401Unauthorized;
                problem.Title = authenticationException.Message;
                break;

            case ForbiddenAccessException forbiddenAccessException:
                problem.Status = StatusCodes.Status403Forbidden;
                problem.Title = forbiddenAccessException.Message;
                break;

            case NotFoundException notFoundException:
                problem.Status = StatusCodes.Status404NotFound;
                problem.Title = notFoundException.Message;
                break;

            case SchedulingConflictException schedulingConflictException:
                problem.Status = StatusCodes.Status409Conflict;
                problem.Title = "A scheduling conflict was detected";
                problem.Extensions["conflicts"] = schedulingConflictException.Conflicts;
                break;

            default:
                problem.Status = StatusCodes.Status500InternalServerError;
                problem.Title = "An unexpected error occurred";
                break;
        }

        httpContext.Response.StatusCode = problem.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}

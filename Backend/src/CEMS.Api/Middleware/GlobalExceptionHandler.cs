using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CEMS.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // The W3C trace id, the same one the audit-logging behavior writes, so a user's error report can be
        // matched to every log line of the request.
        var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

        var problem = new ProblemDetails
        {
            Instance = httpContext.Request.Path
        };

        // Lets a user's error report be matched to the exact log entry.
        problem.Extensions["traceId"] = traceId;

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

        // One line per failed request, levelled by what it means: a 500 is a bug worth an alert, a 401/403 is a
        // security signal (who, and from where), and other client errors are routine.
        var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "anonymous";

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception on {Method} {Path} for user {UserId} (trace {TraceId})",
                httpContext.Request.Method, httpContext.Request.Path, userId, traceId);
        }
        else if (problem.Status is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
        {
            _logger.LogWarning("{Method} {Path} denied with {StatusCode}: {Title} (user {UserId} from {ClientIp}, trace {TraceId})",
                httpContext.Request.Method, httpContext.Request.Path, problem.Status, problem.Title, userId,
                httpContext.Connection.RemoteIpAddress, traceId);
        }
        else
        {
            _logger.LogInformation("{Method} {Path} rejected with {StatusCode}: {Title} (user {UserId}, trace {TraceId})",
                httpContext.Request.Method, httpContext.Request.Path, problem.Status, problem.Title, userId, traceId);
        }

        httpContext.Response.StatusCode = problem.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}

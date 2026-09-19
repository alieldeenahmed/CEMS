using System.Diagnostics;
using CEMS.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CEMS.Application.Common.Behaviors;

/// <summary>
/// Writes one structured log line per state-changing command: who ran it, what it was, whether it
/// succeeded, and how long it took. Only the command's type name is logged, never its payload, so
/// passwords and other sensitive fields can't leak into logs. Queries are skipped to keep read traffic quiet.
/// </summary>
public class AuditLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<AuditLoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUser;

    public AuditLoggingBehavior(ILogger<AuditLoggingBehavior<TRequest, TResponse>> logger, ICurrentUserService currentUser)
    {
        _logger = logger;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var command = typeof(TRequest).Name;

        if (!command.EndsWith("Command", StringComparison.Ordinal))
        {
            return await next();
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();

            _logger.LogInformation(
                "Command {Command} succeeded for user {UserId} ({Roles}) in {ElapsedMs} ms",
                command, _currentUser.UserId, string.Join(",", _currentUser.Roles), stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Command {Command} failed for user {UserId} ({Roles}) with {ErrorType} in {ElapsedMs} ms",
                command, _currentUser.UserId, string.Join(",", _currentUser.Roles), ex.GetType().Name, stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}

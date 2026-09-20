using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using CEMS.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CEMS.Application.Common.Behaviors;

/// <summary>
/// The audit trail: one structured line per state-changing command saying who ran it, what it was, which
/// records it targeted, and how long it took. Queries are skipped to keep read traffic quiet.
///
/// What is logged is deliberately narrow. The command's type name and the values of its Guid properties (the
/// ids of the records it acts on) are logged; nothing else about the payload is, so passwords, tokens, names,
/// emails and amounts can never reach a log by accident. That is enforced by construction (only Guid
/// properties are read) rather than by hoping every command author remembers to keep secrets out.
///
/// Failures are logged here at Debug only. The HTTP layer (GlobalExceptionHandler) already writes exactly one
/// line per failed request -- Warning for 401/403, Information for other client errors, Error for a 500 -- and
/// logging it again at the same level would double every failure. The trace id ties the two together.
/// </summary>
public class AuditLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> IdProperties = new();

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
                "Command {Command} succeeded for user {UserId} ({Roles}) targeting {Targets} in {ElapsedMs} ms (trace {TraceId})",
                command, _currentUser.UserId, string.Join(",", _currentUser.Roles), DescribeTargets(request),
                stopwatch.ElapsedMilliseconds, Activity.Current?.TraceId.ToString());

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                "Command {Command} failed for user {UserId} ({Roles}) targeting {Targets} with {ErrorType} in {ElapsedMs} ms (trace {TraceId})",
                command, _currentUser.UserId, string.Join(",", _currentUser.Roles), DescribeTargets(request), ex.GetType().Name,
                stopwatch.ElapsedMilliseconds, Activity.Current?.TraceId.ToString());

            throw;
        }
    }

    /// <summary>"CourseId=..., StudentId=..." for the request's Guid properties; never any other kind of value.</summary>
    private static string DescribeTargets(TRequest request)
    {
        var properties = IdProperties.GetOrAdd(typeof(TRequest), type => type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(Guid) || p.PropertyType == typeof(Guid?))
            .ToArray());

        var targets = properties
            .Select(p => (p.Name, Value: p.GetValue(request)))
            .Where(t => t.Value is not null)
            .Select(t => $"{t.Name}={t.Value}");

        var described = string.Join(", ", targets);
        return described.Length == 0 ? "-" : described;
    }
}

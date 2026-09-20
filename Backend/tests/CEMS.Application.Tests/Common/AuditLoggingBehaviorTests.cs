using CEMS.Application.Common.Behaviors;
using CEMS.Application.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CEMS.Application.Tests.Common;

public class AuditLoggingBehaviorTests
{
    private record ResetSomethingCommand(string Password) : IRequest<string>;
    private record GetSomethingQuery(string Password) : IRequest<string>;

    private class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private readonly TestCurrentUserService _user = new()
    {
        UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Roles = ["BranchManager"]
    };

    [Fact]
    public async Task Command_Succeeds_LogsWhoRanItAtInformation()
    {
        var logger = new CapturingLogger<AuditLoggingBehavior<ResetSomethingCommand, string>>();
        var behavior = new AuditLoggingBehavior<ResetSomethingCommand, string>(logger, _user);

        var result = await behavior.Handle(new ResetSomethingCommand("hunter2"), _ => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", result);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("ResetSomethingCommand", entry.Message);
        Assert.Contains("11111111-1111-1111-1111-111111111111", entry.Message);
        Assert.Contains("BranchManager", entry.Message);
        Assert.Contains("succeeded", entry.Message);
    }

    [Fact]
    public async Task Command_Fails_RethrowsTheOriginalException_AndLogsOnlyAtDebug_BecauseTheHttpLayerLogsTheFailure()
    {
        var logger = new CapturingLogger<AuditLoggingBehavior<ResetSomethingCommand, string>>();
        var behavior = new AuditLoggingBehavior<ResetSomethingCommand, string>(logger, _user);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(new ResetSomethingCommand("hunter2"), _ => throw new InvalidOperationException("boom"), CancellationToken.None));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);   // one Warning/Information/Error per failure, written by GlobalExceptionHandler
        Assert.Contains("failed", entry.Message);
        Assert.Contains("InvalidOperationException", entry.Message);
    }

    [Fact]
    public async Task Command_NeverLogsThePayload()
    {
        var logger = new CapturingLogger<AuditLoggingBehavior<ResetSomethingCommand, string>>();
        var behavior = new AuditLoggingBehavior<ResetSomethingCommand, string>(logger, _user);

        await behavior.Handle(new ResetSomethingCommand("hunter2"), _ => Task.FromResult("ok"), CancellationToken.None);

        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("hunter2"));
    }

    private record ChangeSomethingCommand(Guid StudentId, Guid? CourseId, Guid? Unset, string Email, string Password, decimal Amount) : IRequest<string>;

    [Fact]
    public async Task Command_LogsTheIdsOfTheRecordsItTargeted_AndNothingElseAboutThePayload()
    {
        var logger = new CapturingLogger<AuditLoggingBehavior<ChangeSomethingCommand, string>>();
        var behavior = new AuditLoggingBehavior<ChangeSomethingCommand, string>(logger, _user);
        var student = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var course = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        await behavior.Handle(new ChangeSomethingCommand(student, course, null, "mariam@codecamp.demo", "S3cret-Pass!", 1234.56m),
            _ => Task.FromResult("ok"), CancellationToken.None);

        var message = Assert.Single(logger.Entries).Message;
        Assert.Contains($"StudentId={student}", message);
        Assert.Contains($"CourseId={course}", message);
        Assert.DoesNotContain("Unset", message);            // a null id is omitted
        Assert.DoesNotContain("mariam@codecamp.demo", message);
        Assert.DoesNotContain("S3cret-Pass!", message);
        Assert.DoesNotContain("1234.56", message);
    }

    [Fact]
    public async Task Command_WithNoIdProperties_StillLogs_WithADashForTargets()
    {
        var logger = new CapturingLogger<AuditLoggingBehavior<ResetSomethingCommand, string>>();
        var behavior = new AuditLoggingBehavior<ResetSomethingCommand, string>(logger, _user);

        await behavior.Handle(new ResetSomethingCommand("hunter2"), _ => Task.FromResult("ok"), CancellationToken.None);

        Assert.Contains("targeting -", Assert.Single(logger.Entries).Message);
    }

    [Fact]
    public async Task Query_IsPassedThroughWithoutLogging()
    {
        var logger = new CapturingLogger<AuditLoggingBehavior<GetSomethingQuery, string>>();
        var behavior = new AuditLoggingBehavior<GetSomethingQuery, string>(logger, _user);

        var result = await behavior.Handle(new GetSomethingQuery("x"), _ => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Empty(logger.Entries);
    }
}

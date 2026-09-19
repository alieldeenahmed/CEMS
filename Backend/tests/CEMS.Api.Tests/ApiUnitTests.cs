using System.Security.Claims;
using System.Text.Json;
using CEMS.Api.Middleware;
using CEMS.Api.Services;
using CEMS.Application.Common.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CEMS.Api.Tests;

public class GlobalExceptionHandlerTests
{
    private class CapturingLogger : ILogger<GlobalExceptionHandler>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }

    private readonly CapturingLogger _logger = new();

    private async Task<(int Status, JsonElement Body)> Handle(Exception exception)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-123" };
        context.Request.Method = "POST";
        context.Request.Path = "/api/things";
        context.Response.Body = new MemoryStream();

        var handled = await new GlobalExceptionHandler(_logger).TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, doc.RootElement.Clone());
    }

    [Fact]
    public async Task ValidationException_Is400_GroupingMessagesByProperty()
    {
        var failures = new[] { new ValidationFailure("Name", "Name is required."), new ValidationFailure("Name", "Name is too short."), new ValidationFailure("Phone", "Phone is required.") };

        var (status, body) = await Handle(new ValidationException(failures));

        Assert.Equal(400, status);
        Assert.Equal("Validation failed", body.GetProperty("title").GetString());
        Assert.Equal(2, body.GetProperty("errors").GetProperty("Name").GetArrayLength());
        Assert.Equal(1, body.GetProperty("errors").GetProperty("Phone").GetArrayLength());
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    public async Task EachDomainException_MapsToItsStatus_WithTheTraceIdAndPath(int expected)
    {
        Exception exception = expected switch
        {
            400 => new BadRequestException(["Something is wrong."]),
            401 => new AuthenticationException("Invalid email or password."),
            403 => new ForbiddenAccessException("Not yours."),
            404 => new NotFoundException("Student", Guid.NewGuid()),
            _ => new SchedulingConflictException(["Room taken."])
        };

        var (status, body) = await Handle(exception);

        Assert.Equal(expected, status);
        Assert.Equal("trace-123", body.GetProperty("traceId").GetString());
        Assert.Equal("/api/things", body.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task SchedulingConflict_ExposesTheConflictList_AndBadRequestItsErrors()
    {
        var (_, conflict) = await Handle(new SchedulingConflictException(["Room taken.", "Teacher busy."]));
        var (_, bad) = await Handle(new BadRequestException(["Nope."]));

        Assert.Equal(["Room taken.", "Teacher busy."], conflict.GetProperty("conflicts").EnumerateArray().Select(e => e.GetString()).ToArray());
        Assert.Equal(["Nope."], bad.GetProperty("errors").EnumerateArray().Select(e => e.GetString()).ToArray());
    }

    [Fact]
    public async Task AnUnexpectedException_Is500_WithAGenericMessage_ThatNeverLeaksTheRealOne()
    {
        var (status, body) = await Handle(new InvalidOperationException("connection string password=hunter2 failed"));

        Assert.Equal(500, status);
        Assert.Equal("An unexpected error occurred", body.GetProperty("title").GetString());
        Assert.DoesNotContain("hunter2", body.ToString());
    }

    [Fact]
    public async Task OnlyA500IsLoggedAsAnError_WithTheExceptionAttached_ExpectedRejectionsAreInformation()
    {
        var boom = new InvalidOperationException("boom");

        await Handle(new NotFoundException("Student", Guid.NewGuid()));
        await Handle(boom);

        var rejected = _logger.Entries[0];
        Assert.Equal(LogLevel.Information, rejected.Level);
        Assert.Null(rejected.Exception);
        Assert.Contains("404", rejected.Message);

        var failure = _logger.Entries[1];
        Assert.Equal(LogLevel.Error, failure.Level);
        Assert.Same(boom, failure.Exception);
        Assert.Contains("trace-123", failure.Message);
    }
}

public class CurrentUserServiceTests
{
    private static CurrentUserService For(params Claim[] claims)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
        return new CurrentUserService(new HttpContextAccessor { HttpContext = context });
    }

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid Smouha = Guid.NewGuid();
    private static readonly Guid KafrAbdo = Guid.NewGuid();

    [Fact]
    public void ReadsTheUserIdRolesAndBranchesFromTheTokenClaims()
    {
        var user = For(
            new Claim("sub", UserId.ToString()),
            new Claim(ClaimTypes.Role, "BranchManager"),
            new Claim("branch_id", Smouha.ToString()));

        Assert.Equal(UserId, user.UserId);
        Assert.Equal(["BranchManager"], user.Roles);
        Assert.Equal([Smouha], user.BranchIds);
        Assert.True(user.IsInRole("BranchManager"));
        Assert.False(user.IsInRole("Owner"));
    }

    [Fact]
    public void ABranchUserHasAccessOnlyToTheirOwnBranches()
    {
        var user = For(new Claim(ClaimTypes.Role, "FrontDesk"), new Claim("branch_id", Smouha.ToString()));

        Assert.True(user.HasAccessToBranch(Smouha));
        Assert.False(user.HasAccessToBranch(KafrAbdo));
    }

    [Fact]
    public void TheOwnerHasAccessToEveryBranchWithoutListingAny()
    {
        var owner = For(new Claim(ClaimTypes.Role, "Owner"));

        Assert.True(owner.HasAccessToBranch(Smouha));
        Assert.True(owner.HasAccessToBranch(Guid.NewGuid()));
    }

    [Fact]
    public void AFloatingTeacher_WithSeveralBranchClaims_HasAccessToAllOfThem()
    {
        var teacher = For(new Claim(ClaimTypes.Role, "Teacher"), new Claim("branch_id", Smouha.ToString()), new Claim("branch_id", KafrAbdo.ToString()));

        Assert.True(teacher.HasAccessToBranch(Smouha));
        Assert.True(teacher.HasAccessToBranch(KafrAbdo));
    }

    [Fact]
    public void WithoutAUser_EverythingIsEmptyAndNoAccessIsGranted()
    {
        var nobody = new CurrentUserService(new HttpContextAccessor { HttpContext = null });

        Assert.Null(nobody.UserId);
        Assert.Empty(nobody.Roles);
        Assert.Empty(nobody.BranchIds);
        Assert.False(nobody.HasAccessToBranch(Smouha));
    }

    [Fact]
    public void AMalformedSubjectClaim_YieldsNoUserIdInsteadOfThrowing()
    {
        var user = For(new Claim("sub", "not-a-guid"));

        Assert.Null(user.UserId);
    }
}

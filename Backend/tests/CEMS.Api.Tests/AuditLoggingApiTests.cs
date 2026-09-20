using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CEMS.Api.Tests;

/// <summary>What the running application writes to its logs -- captured from the real pipeline, not a stub.</summary>
public class AuditLoggingApiTests : IClassFixture<ApiWorld>
{
    private readonly ApiWorld _w;

    public AuditLoggingApiTests(ApiWorld world) => _w = world;

    private IReadOnlyList<LogLine> Snapshot() => _w.Logs.Lines;

    private static string TraceIdOf(string problemJson) => JsonDocument.Parse(problemJson).RootElement.GetProperty("traceId").GetString()!;

    [Fact]
    public async Task NoSecret_EverReachesTheLogs_AcrossLoginPasswordChangesAndStaffCreation()
    {
        const string newPassword = "Brand-New-Secret-9!";
        const string loginPassword = "Sup3r-Secret-Login!";
        var email = $"logging.{Guid.NewGuid():N}@codecamp.demo";

        var created = await _w.Owner.PostAsJsonAsync("/api/users/staff",
            new { email, password = loginPassword, fullName = "Log Test", phoneNumber = "01000000000", role = "FrontDesk", branchId = _w.Smouha });
        var userId = (await ApiWorld.ReadJsonAsync(created)).GetProperty("userId").GetGuid();
        var token = await ReadTokenAsync(await _w.Anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = loginPassword }));
        await _w.Anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = "a-wrong-guess-Zx81" });
        await _w.Owner.PostAsJsonAsync($"/api/users/staff/{userId}/reset-password", new { newPassword });

        var everything = string.Join('\n', Snapshot().Select(l => l.Message + (l.Exception?.ToString() ?? "")));
        foreach (var secret in new[] { newPassword, loginPassword, "a-wrong-guess-Zx81", token, email })
        {
            Assert.DoesNotContain(secret, everything);
        }
    }

    [Fact]
    public async Task AMutation_IsAuditedWithWhoDidItAndWhichRecordItTargeted()
    {
        var before = Snapshot().Count;
        var target = Guid.NewGuid().ToString();

        var room = await _w.CreateAsync($"/api/branches/{_w.Smouha}/rooms", new { name = $"Audit {target[..6]}", capacity = 3 }, _w.ManagerSmouha);

        var lines = Snapshot().Skip(before).ToList();
        var audit = Assert.Single(lines, l => l.Message.StartsWith("Command CreateRoomCommand succeeded"));
        Assert.Equal(LogLevel.Information, audit.Level);
        Assert.Contains("BranchManager", audit.Message);
        Assert.Contains($"BranchId={_w.Smouha}", audit.Message);
        Assert.NotEqual(Guid.Empty, room);
    }

    [Fact]
    public async Task ADeniedRequest_IsOneWarning_NamingTheUserAndThePath_NotTwoLines()
    {
        var before = Snapshot().Count;

        var response = await _w.ManagerKafrAbdo.GetAsync($"/api/students/{_w.TaughtStudent}");   // another branch's student

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var traceId = TraceIdOf(await response.Content.ReadAsStringAsync());
        var lines = Snapshot().Skip(before).Where(l => l.Message.Contains(traceId)).ToList();
        var denial = Assert.Single(lines);   // the failure is logged once, not once per layer
        Assert.Equal(LogLevel.Warning, denial.Level);
        Assert.Contains("403", denial.Message);
        Assert.Contains($"/api/students/{_w.TaughtStudent}", denial.Message);
        Assert.DoesNotContain("anonymous", denial.Message);   // the user id is there
    }

    [Fact]
    public async Task AFailedLogin_IsAWarning_WithoutTheEmailOrTheGuess()
    {
        var before = Snapshot().Count;

        var response = await _w.Anonymous.PostAsJsonAsync("/api/auth/login", new { email = "someone.private@example.com", password = "Guess-1234" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var lines = Snapshot().Skip(before).ToList();
        var denial = Assert.Single(lines, l => l.Message.Contains("401"));
        Assert.Equal(LogLevel.Warning, denial.Level);
        Assert.Contains("anonymous", denial.Message);
        Assert.DoesNotContain(lines, l => l.Message.Contains("someone.private@example.com") || l.Message.Contains("Guess-1234"));
    }

    [Fact]
    public async Task AClientError_IsInformation_AndAServerFaultWouldBeAnError()
    {
        var before = Snapshot().Count;

        var response = await _w.ManagerSmouha.PostAsJsonAsync($"/api/courses/{_w.Course}/sessions",
            ApiWorld.SessionBody(_w.SmouhaRoom, _w.TeacherId, ApiWorld.SessionStart));   // already booked

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var traceId = TraceIdOf(await response.Content.ReadAsStringAsync());
        var line = Assert.Single(Snapshot().Skip(before).Where(l => l.Message.Contains(traceId)));
        Assert.Equal(LogLevel.Information, line.Level);
        Assert.Null(line.Exception);   // no stack trace for an expected rejection
    }

    private static async Task<string> ReadTokenAsync(HttpResponseMessage response) =>
        (await ApiWorld.ReadJsonAsync(response)).GetProperty("token").GetString()!;
}

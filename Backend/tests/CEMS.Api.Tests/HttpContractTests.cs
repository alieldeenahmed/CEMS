using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;

namespace CEMS.Api.Tests;

/// <summary>What a client actually receives: error shapes, scheduling conflicts, CORS, and Swagger.</summary>
public class HttpContractTests : IClassFixture<ApiWorld>
{
    private readonly ApiWorld _w;

    public HttpContractTests(ApiWorld world) => _w = world;

    // ---- Error responses ----

    [Fact]
    public async Task NotFound_IsA404ProblemWithTheRequestPathAndATraceId()
    {
        var id = Guid.NewGuid();

        var response = await _w.Owner.GetAsync($"/api/students/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ApiWorld.ReadJsonAsync(response);
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.Contains("Student", problem.GetProperty("title").GetString());
        Assert.Equal($"/api/students/{id}", problem.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task ValidationFailure_IsA400ListingEveryBadField()
    {
        var response = await _w.Owner.PostAsJsonAsync("/api/branches", new { name = "", address = "", phone = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ApiWorld.ReadJsonAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Name", out _));
        Assert.True(errors.TryGetProperty("Address", out _));
        Assert.True(errors.TryGetProperty("Phone", out _));
    }

    [Fact]
    public async Task BusinessRuleViolation_IsA400WithAReadableMessage()
    {
        var response = await _w.Owner.PostAsJsonAsync($"/api/students/{_w.TaughtStudent}/transfer-branch",
            new { newBranchId = _w.Smouha, reason = "same branch" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ApiWorld.ReadJsonAsync(response);
        Assert.Equal("Bad request", problem.GetProperty("title").GetString());
        Assert.NotEmpty(problem.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task DeletingSomethingWithDependents_IsAReadable400_NotAServerError()
    {
        // The taught student has an enrollment, so the delete is refused with an explanation.
        var response = await _w.Owner.DeleteAsync($"/api/students/{_w.TaughtStudent}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Paused or Graduated", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/students/not-a-guid")]
    [InlineData("/api/does-not-exist")]
    public async Task UnknownOrMalformedRoutes_Return404(string path)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _w.Owner.GetAsync(path)).StatusCode);
    }

    // ---- Scheduling conflicts over HTTP ----

    [Fact]
    public async Task DoubleBooking_Returns409WithEveryConflictListed()
    {
        var response = await _w.FrontDeskSmouha.PostAsJsonAsync($"/api/courses/{_w.Course}/sessions",
            ApiWorld.SessionBody(_w.SmouhaRoom, _w.TeacherId, ApiWorld.SessionStart));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ApiWorld.ReadJsonAsync(response);
        Assert.Equal("A scheduling conflict was detected", problem.GetProperty("title").GetString());
        var conflicts = problem.GetProperty("conflicts").EnumerateArray().Select(c => c.GetString()).ToArray();
        Assert.Contains(conflicts, c => c!.Contains("room"));
        Assert.Contains(conflicts, c => c!.Contains("teacher is already booked"));
    }

    [Fact]
    public async Task OverridingAConflict_NeedsAManagerOrOwner_AndAReason()
    {
        // The world's session already occupies this exact slot, so every request below is a genuine conflict.
        var frontDesk = await _w.FrontDeskSmouha.PostAsJsonAsync($"/api/courses/{_w.Course}/sessions",
            ApiWorld.SessionBody(_w.SmouhaRoom, _w.TeacherId, ApiWorld.SessionStart, @override: true, reason: "please"));
        var noReason = await _w.ManagerSmouha.PostAsJsonAsync($"/api/courses/{_w.Course}/sessions",
            ApiWorld.SessionBody(_w.SmouhaRoom, _w.TeacherId, ApiWorld.SessionStart, @override: true, reason: ""));
        var withReason = await _w.ManagerSmouha.PostAsJsonAsync($"/api/courses/{_w.Course}/sessions",
            ApiWorld.SessionBody(_w.SmouhaRoom, _w.TeacherId, ApiWorld.SessionStart, @override: true, reason: "Extra revision class"));

        Assert.Equal(HttpStatusCode.Forbidden, frontDesk.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.Created, withReason.StatusCode);
        var session = await ApiWorld.ReadJsonAsync(withReason);
        Assert.True(session.GetProperty("overridden").GetBoolean());
        Assert.Equal("Extra revision class", session.GetProperty("overrideReason").GetString());
    }

    [Fact]
    public async Task ARoomFromAnotherBranch_IsRefusedEvenToTheOwnerWithAnOverride()
    {
        var response = await _w.Owner.PostAsJsonAsync($"/api/courses/{_w.Course}/sessions",
            ApiWorld.SessionBody(_w.KafrAbdoRoom, _w.TeacherId, ApiWorld.SessionStart.AddDays(30), @override: true, reason: "try"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- CORS ----

    [Fact]
    public async Task Cors_AllowsTheConfiguredFrontendOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/students");
        request.Headers.Add("Origin", CemsApiFactory.AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");

        var response = await _w.Anonymous.SendAsync(request);

        Assert.Equal(CemsApiFactory.AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Cors_GivesAnUnknownOriginNoPermission()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/students");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _w.Anonymous.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    // ---- Swagger is a development tool only ----

    [Fact]
    public async Task Swagger_IsServedInDevelopmentWithBearerAuthDeclared()
    {
        var response = await _w.Anonymous.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"bearer\"", doc.ToLowerInvariant());
        Assert.Contains("/api/students", doc);
    }

    [Fact]
    public async Task Swagger_IsNotExposedInProduction()
    {
        using var production = new ApiWorld().WithWebHostBuilder(b => b.UseEnvironment("Production"));

        var response = await production.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

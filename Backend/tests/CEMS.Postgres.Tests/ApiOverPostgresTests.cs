using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CEMS.Api.Tests;
using CEMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CEMS.Postgres.Tests;

/// <summary>The real API (routing, JWT, MediatR, Identity) with the database swapped for PostgreSQL and the real migrations.</summary>
public sealed class PostgresApiFactory : CemsApiFactory
{
    private PostgresTestDatabase? _database;

    public override async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        await base.InitializeAsync();
    }

    protected override void ConfigureDatabase(IServiceCollection services) =>
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(_database!.ConnectionString));

    protected override Task CreateSchemaAsync(ApplicationDbContext context) => Task.CompletedTask;   // already migrated

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _database?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}

/// <summary>The two-branch demo center from the API test suite, built on PostgreSQL.</summary>
public sealed class PostgresApiWorld : ApiWorld
{
    private PostgresTestDatabase? _database;

    public override async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        await base.InitializeAsync();
    }

    protected override void ConfigureDatabase(IServiceCollection services) =>
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(_database!.ConnectionString));

    protected override Task CreateSchemaAsync(ApplicationDbContext context) => Task.CompletedTask;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _database?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}

public class ApiOverPostgresTests
{
    private static async Task<PostgresApiWorld> NewWorld()
    {
        var world = new PostgresApiWorld();
        await world.InitializeAsync();
        return world;
    }

    [Fact]
    public async Task FiveSimultaneousFirstTimeSetups_CreateExactlyOneOwner()
    {
        using var factory = new PostgresApiFactory();
        await factory.InitializeAsync();

        var responses = await SchedulingWorld.RaceAsync(5, async i =>
        {
            using var client = factory.CreateClient();
            var response = await client.PostAsJsonAsync("/api/auth/bootstrap-owner",
                new { email = $"owner{i}@codecamp.demo", password = "DemoPass123", fullName = "Mostafa El-Sayed", phoneNumber = "01012340001" });
            return response.StatusCode;
        });

        Assert.All(responses, r => Assert.True(r.Succeeded, r.Error?.ToString()));
        Assert.Equal(1, responses.Count(r => r.Value == HttpStatusCode.OK));
        Assert.Equal(4, responses.Count(r => r.Value == HttpStatusCode.Forbidden));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Equal(1, await context.UserRoles.CountAsync());
    }

    [Fact]
    public async Task SixSimultaneousBookingsOfTheSameRoomAndTeacher_OverHttp_ExactlyOneIs201_TheRestAre409()
    {
        using var world = await NewWorld();
        var monday = ApiWorld.SessionStart.AddDays(7);

        var responses = await SchedulingWorld.RaceAsync(6, async _ =>
        {
            var response = await world.FrontDeskSmouha.PostAsJsonAsync(
                $"/api/courses/{world.Course}/sessions", ApiWorld.SessionBody(world.SmouhaRoom, world.TeacherId, monday));
            return (response.StatusCode, Body: await response.Content.ReadAsStringAsync());
        });

        Assert.All(responses, r => Assert.True(r.Succeeded, r.Error?.ToString()));
        Assert.Equal(1, responses.Count(r => r.Value.StatusCode == HttpStatusCode.Created));
        Assert.Equal(5, responses.Count(r => r.Value.StatusCode == HttpStatusCode.Conflict));
        Assert.All(responses.Where(r => r.Value.StatusCode == HttpStatusCode.Conflict), r => Assert.Contains("already booked", r.Value.Body));

        var sessions = await world.FrontDeskSmouha.GetFromJsonAsync<JsonElement>($"/api/courses/{world.Course}/sessions");
        Assert.Equal(2, sessions.GetArrayLength());   // the world's own session plus the single winner
    }

    [Fact]
    public async Task ATimeSentWithAnOffset_IsStoredAndReturnedAsTheSameInstantInUtc()
    {
        using var world = await NewWorld();

        // 12:00 at UTC+2 is 10:00 UTC, which is inside the teacher's Monday 09:00-17:00 window.
        var response = await world.FrontDeskSmouha.PostAsJsonAsync($"/api/courses/{world.Course}/sessions", new
        {
            roomId = world.SmouhaRoom, teacherId = world.TeacherId,
            startUtc = "2030-01-14T12:00:00+02:00", endUtc = "2030-01-14T13:00:00+02:00"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new DateTime(2030, 1, 14, 10, 0, 0, DateTimeKind.Utc), body.GetProperty("startUtc").GetDateTime().ToUniversalTime());
        Assert.False(body.GetProperty("overridden").GetBoolean());   // i.e. it really was treated as 10:00 UTC, inside the window
    }

    [Fact]
    public async Task ATimeSentWithoutAZone_IsTakenAsUtc_AndTheDatabaseAgrees()
    {
        using var world = await NewWorld();

        var response = await world.FrontDeskSmouha.PostAsJsonAsync($"/api/courses/{world.Course}/sessions", new
        {
            roomId = world.SmouhaRoom, teacherId = world.TeacherId,
            startUtc = "2030-01-14T10:00:00", endUtc = "2030-01-14T11:00:00"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Ask PostgreSQL itself what instant it stored, in a session whose time zone is not UTC.
        using var scope = world.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.OpenConnectionAsync();
        await context.Database.ExecuteSqlRawAsync("SET TIME ZONE 'Africa/Cairo'");
        var epoch = await context.Database
            .SqlQuery<double>($"""SELECT extract(epoch FROM "StartUtc")::float8 AS "Value" FROM "CourseSessions" WHERE "StartUtc" = '2030-01-14T10:00:00Z'::timestamptz""")
            .ToListAsync();

        Assert.Single(epoch);
        Assert.Equal(new DateTimeOffset(2030, 1, 14, 10, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), (long)epoch[0]);
    }

    [Fact]
    public async Task TheWholeDemoCenterCanBeBuilt_AndItsDashboardQueriesTranslateOnPostgres()
    {
        using var world = await NewWorld();
        var package = await world.CreateAsync($"/api/courses/{world.Course}/packages", new { sessionCount = 12, price = 2400.50m });
        var invoice = await world.CreateAsync($"/api/students/{world.TaughtStudent}/invoices", new { packageId = package, dueDate = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd") });
        (await world.Owner.PostAsJsonAsync($"/api/invoices/{invoice}/payments",
            new { amountPaid = 1000.25m, paymentDate = DateTime.UtcNow.ToString("yyyy-MM-dd"), method = "Cash" })).EnsureSuccessStatusCode();

        var from = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        var to = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
        var summary = await world.Owner.GetFromJsonAsync<JsonElement>($"/api/analytics/dashboard?periodStart={from}&periodEnd={to}");

        var revenue = summary.GetProperty("revenue");
        Assert.Equal(2400.50m, revenue.GetProperty("totalInvoiced").GetDecimal());
        Assert.Equal(1000.25m, revenue.GetProperty("totalCollected").GetDecimal());
        Assert.Equal(1400.25m, revenue.GetProperty("totalOutstanding").GetDecimal());
        Assert.Equal(3, summary.GetProperty("enrollmentFunnel").GetProperty("totalStudents").GetInt32());   // two at Smouha, one at Kafr Abdo
    }
}

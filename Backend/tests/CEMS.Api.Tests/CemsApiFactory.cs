using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CEMS.Api.Tests;

/// <summary>
/// Boots the real application (real controllers, JWT pipeline, MediatR, Identity, exception handler) in
/// memory, with the Npgsql database swapped for a private SQLite in-memory one. Every test class that
/// uses it gets its own empty database.
/// </summary>
public class CemsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SigningKey = "integration-test-signing-key-that-is-long-enough-for-hmac";
    public const string AllowedOrigin = "http://localhost:5173";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    static CemsApiFactory()
    {
        // Program.cs fails fast without these, and reads them before any factory hook can run, so they
        // are supplied as environment variables (which also outrank a developer's user-secrets).
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", "Host=unused;Database=unused");
        Environment.SetEnvironmentVariable("Jwt__Key", SigningKey);
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", AllowedOrigin);
    }

    public CemsApiFactory()
    {
        _connection.Open();
    }

    /// <summary>Every log line the running app writes, for tests that assert on what is (and is not) logged.</summary>
    public LogSink Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<ApplicationDbContext>));
            ConfigureDatabase(services);
        });
    }

    /// <summary>The database the app talks to; a subclass can point it at PostgreSQL instead.</summary>
    protected virtual void ConfigureDatabase(IServiceCollection services) =>
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));

    /// <summary>Creates the schema. The SQLite database is built from the model; PostgreSQL runs the real migrations.</summary>
    protected virtual Task CreateSchemaAsync(ApplicationDbContext context) => context.Database.EnsureCreatedAsync();

    /// <summary>Creates the schema and the four roles (migrations seed them in production).</summary>
    public virtual async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await CreateSchemaAsync(context);

        // Migrations seed the roles; a schema built from the model does not.
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { "Owner", "BranchManager", "Teacher", "FrontDesk" })
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole<Guid>(role));
            }
        }
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }

    // ---- Helpers for driving the API the way a real client would ----

    public const string OwnerEmail = "owner@codecamp.demo";
    public const string Password = "DemoPass123";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Bootstraps the Owner through the real endpoint and returns a client already signed in as them.</summary>
    public async Task<HttpClient> SignInAsOwnerAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/bootstrap-owner",
            new { email = OwnerEmail, password = Password, fullName = "Mostafa El-Sayed", phoneNumber = "01012340001" });
        response.EnsureSuccessStatusCode();
        return Authorized(await ReadTokenAsync(response));
    }

    public async Task<HttpClient> SignInAsync(string email, string password = Password)
    {
        var response = await CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return Authorized(await ReadTokenAsync(response));
    }

    public HttpClient Authorized(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> ReadTokenAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("token").GetString()!;
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}

using CEMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CEMS.Postgres.Tests;

/// <summary>
/// A throwaway PostgreSQL database, created on a server you already have and dropped afterwards, so a
/// test run never touches development data.
///
/// Where the server comes from, in order:
///   1. the CEMS_TEST_POSTGRES environment variable (a normal Npgsql connection string; CI sets it),
///   2. the API project's own `dotnet user-secrets` ConnectionStrings:Default (so a developer who can
///      already run the app can run these tests with no extra setup) -- only its host, port and
///      credentials are used; the database name is replaced.
/// The role needs permission to CREATE DATABASE. If neither is available -- or the server cannot be
/// reached -- this throws with instructions, so the tests fail loudly rather than pretending PostgreSQL
/// was exercised. Connection strings are never included in messages.
/// </summary>
public sealed class PostgresTestDatabase : IAsyncDisposable
{
    private const string ApiUserSecretsId = "fc40b714-2a7c-46f4-8cad-08d9fe60b2ed";

    private readonly NpgsqlConnectionStringBuilder _admin;

    private PostgresTestDatabase(NpgsqlConnectionStringBuilder admin, string name, string connectionString)
    {
        _admin = admin;
        Name = name;
        ConnectionString = connectionString;
    }

    public string Name { get; }
    public string ConnectionString { get; }

    public static async Task<PostgresTestDatabase> CreateAsync(bool migrate = true)
    {
        var admin = ResolveAdminConnection();
        var name = $"cems_it_{Guid.NewGuid():N}";

        try
        {
            await using var connection = new NpgsqlConnection(admin.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex) when (ex is NpgsqlException or System.Net.Sockets.SocketException or TimeoutException)
        {
            throw Unavailable($"could not create a test database on {admin.Host}:{admin.Port} ({ex.GetType().Name}: {ex.Message})");
        }

        var databaseConnection = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = name };
        var database = new PostgresTestDatabase(admin, name, databaseConnection.ConnectionString);

        if (migrate)
        {
            await using var context = database.CreateContext();
            await context.Database.MigrateAsync();
        }

        return database;
    }

    public ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(ConnectionString).Options);

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(_admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static NpgsqlConnectionStringBuilder ResolveAdminConnection()
    {
        var connectionString = Environment.GetEnvironmentVariable("CEMS_TEST_POSTGRES");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = new ConfigurationBuilder().AddUserSecrets(ApiUserSecretsId).Build()["ConnectionStrings:Default"];
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw Unavailable("no server is configured");
        }

        // Always connect to the maintenance database to create/drop the throwaway one.
        return new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres", Pooling = false };
    }

    private static InvalidOperationException Unavailable(string reason) => new(
        $"PostgreSQL integration tests need a PostgreSQL server, but {reason}.\n" +
        "Provide one and set CEMS_TEST_POSTGRES to a connection string whose role can CREATE DATABASE, e.g.\n" +
        "  docker run -d --name cems-pg -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:16\n" +
        "  CEMS_TEST_POSTGRES=\"Host=localhost;Username=postgres;Password=postgres\"\n" +
        "(or configure the API's ConnectionStrings:Default with `dotnet user-secrets`). " +
        "To run only the hermetic suites: dotnet test --filter \"FullyQualifiedName!~CEMS.Postgres.Tests\".");
}

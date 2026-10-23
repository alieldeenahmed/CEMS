using CEMS.Api.Tests;
using CEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CEMS.Postgres.Tests;

/// <summary>
/// The Docker Compose demo starts the API against an empty database and relies on the opt-in
/// <c>Database:MigrateOnStartup</c> flag to build the schema; without the flag the API must never touch it.
/// </summary>
public class MigrateOnStartupTests
{
    private sealed class EmptyDatabaseApi(PostgresTestDatabase database, bool migrateOnStartup) : CemsApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Database:MigrateOnStartup", migrateOnStartup ? "true" : "false");
        }

        protected override void ConfigureDatabase(IServiceCollection services) =>
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(database.ConnectionString));
    }

    [Fact]
    public async Task WithTheFlagOn_StartingTheApi_AppliesEveryMigrationToAnEmptyDatabase()
    {
        await using var database = await PostgresTestDatabase.CreateAsync(migrate: false);
        using var api = new EmptyDatabaseApi(database, migrateOnStartup: true);

        _ = api.Services;   // builds and starts the host, which runs Program.cs

        await using var context = database.CreateContext();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Contains("AddEnrollmentIndexesAndCheckConstraints", string.Join(',', await context.Database.GetAppliedMigrationsAsync()));
    }

    [Fact]
    public async Task WithTheFlagOff_StartingTheApi_LeavesTheDatabaseUntouched()
    {
        await using var database = await PostgresTestDatabase.CreateAsync(migrate: false);
        using var api = new EmptyDatabaseApi(database, migrateOnStartup: false);

        _ = api.Services;

        await using var context = database.CreateContext();
        Assert.Empty(await context.Database.GetAppliedMigrationsAsync());
    }
}

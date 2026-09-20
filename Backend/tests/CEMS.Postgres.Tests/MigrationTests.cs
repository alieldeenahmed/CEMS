using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CEMS.Postgres.Tests;

/// <summary>
/// The fast suites build their SQLite schema from the EF model, so nothing else ever runs the migrations
/// that production actually uses. These do.
/// </summary>
public class MigrationTests
{
    [Fact]
    public async Task EveryMigration_AppliesToAnEmptyDatabase_AndTheModelHasNoUnmigratedChanges()
    {
        await using var database = await PostgresTestDatabase.CreateAsync(migrate: false);
        await using var context = database.CreateContext();

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());

        // If someone edits an entity or configuration without adding a migration, production would drift
        // from what the tests believe the schema is.
        Assert.False(context.Database.HasPendingModelChanges(),
            "The EF model differs from the last migration's snapshot. Add a migration (dotnet ef migrations add).");
    }

    [Fact]
    public async Task TheFourRolesAreSeededByMigrations()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = database.CreateContext();

        var roles = await context.Roles.Select(r => r.Name!).OrderBy(n => n).ToListAsync();

        Assert.Equal(new[] { "BranchManager", "FrontDesk", "Owner", "Teacher" }, roles);
    }

    [Fact]
    public async Task TheSchedulingConstraintsExistInTheDatabase()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = database.CreateContext();

        var constraints = await context.Database
            .SqlQuery<string>($"""
                SELECT conname AS "Value" FROM pg_constraint
                WHERE conrelid = '"CourseSessions"'::regclass AND contype IN ('x', 'c')
                """)
            .ToListAsync();

        Assert.Contains("ex_course_sessions_room_no_overlap", constraints);
        Assert.Contains("ex_course_sessions_teacher_no_overlap", constraints);
        Assert.Contains("ck_course_sessions_end_after_start", constraints);
    }

    [Fact]
    public async Task TheLatestMigration_CanBeRolledBackAndReapplied()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var migrator = context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();
        var previous = applied[^2];

        await migrator.MigrateAsync(previous);
        Assert.Equal(previous, (await context.Database.GetAppliedMigrationsAsync()).Last());

        await context.Database.MigrateAsync();
        Assert.Equal(applied.Last(), (await context.Database.GetAppliedMigrationsAsync()).Last());
    }
}

using CEMS.Application.Users.Commands.CreateStaffUser;
using CEMS.Domain.Branches;
using CEMS.Domain.Users;
using CEMS.Application.Tests.TestSupport;
using CEMS.Infrastructure.Identity;
using CEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CEMS.Application.Tests.Infrastructure;

/// <summary>
/// Operations that write more than once (an Identity account, its role, its branch assignment) must leave
/// nothing behind when a later write fails. Uses the real Identity stack, because Identity commits each of its
/// own writes separately unless a transaction is open around them.
/// </summary>
public class TransactionBoundaryTests : IDisposable
{
    /// <summary>Fails any save that tries to add a branch assignment, standing in for a database error part-way through.</summary>
    private sealed class FailOnBranchAssignment : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<UserBranchAssignment>().Any(e => e.State == EntityState.Added))
            {
                throw new DbUpdateException("Simulated failure while saving the branch assignment.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly ApplicationDbContext _context;
    private readonly IdentityService _identity;
    private readonly FailOnBranchAssignment _failure = new();
    private readonly Branch _branch = new() { Id = Guid.NewGuid(), Name = "Smouha" };

    public TransactionBoundaryTests()
    {
        _connection.Open();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection).AddInterceptors(_failure));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<ApplicationDbContext>();
        services.Configure<IdentityOptions>(o => { o.Password.RequiredLength = 8; o.Password.RequireNonAlphanumeric = false; o.Password.RequireUppercase = false; o.Password.RequireLowercase = false; o.Password.RequireDigit = false; });

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _context.Database.EnsureCreated();

        var roles = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { RoleNames.Owner, RoleNames.BranchManager, RoleNames.Teacher, RoleNames.FrontDesk })
        {
            roles.CreateAsync(new IdentityRole<Guid>(role)).GetAwaiter().GetResult();
        }

        _context.Branches.Add(_branch);
        _context.SaveChanges();
        _identity = new IdentityService(_scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), _context);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _connection.Dispose();
    }

    private CreateStaffUserCommandHandler Handler() =>
        new(_context, _identity, new TestCurrentUserService { Roles = [RoleNames.Owner] });

    private static CreateStaffUserCommand Command() =>
        new("mariam@codecamp.demo", "DemoPass123", "Mariam Younis", "01000000000", RoleNames.FrontDesk, null);

    [Fact]
    public async Task CreatingAStaffMember_WritesTheAccount_TheRole_AndTheBranchAssignmentTogether()
    {
        var staff = await Handler().Handle(Command() with { BranchId = _branch.Id }, CancellationToken.None);

        Assert.Equal(new[] { RoleNames.FrontDesk }, staff.Roles);
        Assert.Equal(new[] { _branch.Id }, staff.BranchIds);
    }

    [Fact]
    public async Task IfTheBranchAssignmentCannotBeSaved_TheAccountIsNotLeftBehind()
    {
        _failure.Enabled = true;

        await Assert.ThrowsAsync<DbUpdateException>(() => Handler().Handle(Command() with { BranchId = _branch.Id }, CancellationToken.None));

        // Without the transaction the login already existed by now: it could sign in, saw nothing (no branch),
        // and retrying failed with "email already taken".
        _context.ChangeTracker.Clear();
        Assert.Empty(_context.Users);
        Assert.Empty(_context.UserRoles);
        Assert.Empty(_context.UserBranchAssignments);
    }

    [Fact]
    public async Task AfterAFailedAttempt_TheSameEmailCanBeUsedAgain()
    {
        _failure.Enabled = true;
        await Assert.ThrowsAsync<DbUpdateException>(() => Handler().Handle(Command() with { BranchId = _branch.Id }, CancellationToken.None));
        _failure.Enabled = false;
        _context.ChangeTracker.Clear();

        var staff = await Handler().Handle(Command() with { BranchId = _branch.Id }, CancellationToken.None);

        Assert.Equal("mariam@codecamp.demo", staff.Email);
    }

    [Fact]
    public async Task CreatingAnAccountWithARoleThatDoesNotExist_FailsAndLeavesNoAccountBehind()
    {
        // Identity throws for an unknown role -- after it has already saved the user. The surrounding
        // transaction is what takes that user back out.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _identity.CreateUserAsync("x@codecamp.demo", "DemoPass123", "X", "010", "NoSuchRole"));

        _context.ChangeTracker.Clear();
        Assert.Empty(_context.Users);
    }
}

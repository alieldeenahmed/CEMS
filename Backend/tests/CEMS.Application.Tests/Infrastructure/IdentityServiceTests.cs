using CEMS.Application.Common.Exceptions;
using CEMS.Domain.Branches;
using CEMS.Domain.Users;
using CEMS.Infrastructure.Identity;
using CEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CEMS.Application.Tests.Infrastructure;

/// <summary>
/// Runs the real IdentityService on the real ASP.NET Identity stack (UserManager, password hashing and
/// policy) over SQLite, configured with the same password rules as production.
/// </summary>
public class IdentityServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly ApplicationDbContext _context;
    private readonly IdentityService _service;

    public IdentityServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireDigit = false;
            options.Password.RequiredUniqueChars = 1;
        });

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _context.Database.EnsureCreated();

        var roles = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { RoleNames.Owner, RoleNames.BranchManager, RoleNames.Teacher, RoleNames.FrontDesk })
        {
            roles.CreateAsync(new IdentityRole<Guid>(role)).GetAwaiter().GetResult();
        }

        _service = new IdentityService(_scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), _context);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<Guid> Create(string email = "mariam@codecamp.demo", string password = "DemoPass123", string role = RoleNames.FrontDesk, string name = "Mariam Younis")
    {
        var result = await _service.CreateUserAsync(email, password, name, "01000000000", role);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        return result.UserId;
    }

    // ---- Create ----

    [Fact]
    public async Task CreateUser_StoresTheUserInTheRequestedRole_WithAHashedPassword()
    {
        var id = await Create(role: RoleNames.Teacher);

        var user = await _service.GetAuthenticatedUserAsync(id);

        Assert.Equal([RoleNames.Teacher], user.Roles);
        Assert.Equal("Mariam Younis", user.FullName);
        Assert.True(user.IsActive);
        var stored = await _context.Users.SingleAsync(u => u.Id == id);
        Assert.DoesNotContain("DemoPass123", stored.PasswordHash);
    }

    [Fact]
    public async Task CreateUser_RejectsADuplicateEmail_AndAPasswordThatBreaksThePolicy()
    {
        await Create();

        var duplicate = await _service.CreateUserAsync("mariam@codecamp.demo", "DemoPass123", "Other", "0100", RoleNames.FrontDesk);
        var tooShort = await _service.CreateUserAsync("new@codecamp.demo", "short", "New", "0100", RoleNames.FrontDesk);

        Assert.False(duplicate.Succeeded);
        Assert.NotEmpty(duplicate.Errors);
        Assert.False(tooShort.Succeeded);
        Assert.Single(_context.Users);
    }

    // ---- Login ----

    [Fact]
    public async Task ValidateCredentials_WithTheRightPassword_ReturnsRolesAndBranches()
    {
        var id = await Create(role: RoleNames.BranchManager);
        var branch = new Branch { Id = Guid.NewGuid(), Name = "Smouha" };
        _context.Branches.Add(branch);
        _context.UserBranchAssignments.Add(new UserBranchAssignment { Id = Guid.NewGuid(), UserId = id, BranchId = branch.Id });
        await _context.SaveChangesAsync();

        var user = await _service.ValidateCredentialsAsync("mariam@codecamp.demo", "DemoPass123");

        Assert.NotNull(user);
        Assert.Equal([RoleNames.BranchManager], user.Roles);
        Assert.Equal([branch.Id], user.BranchIds);
    }

    [Theory]
    [InlineData("mariam@codecamp.demo", "WrongPass123")]
    [InlineData("ghost@codecamp.demo", "DemoPass123")]
    public async Task ValidateCredentials_WrongPasswordOrUnknownEmail_ReturnsNull(string email, string password)
    {
        await Create();

        Assert.Null(await _service.ValidateCredentialsAsync(email, password));
    }

    [Fact]
    public async Task ValidateCredentials_ADeactivatedUserCannotLogInEvenWithTheRightPassword_UntilReactivated()
    {
        var id = await Create();

        await _service.SetUserActiveAsync(id, false);
        Assert.Null(await _service.ValidateCredentialsAsync("mariam@codecamp.demo", "DemoPass123"));

        await _service.SetUserActiveAsync(id, true);
        Assert.NotNull(await _service.ValidateCredentialsAsync("mariam@codecamp.demo", "DemoPass123"));
    }

    [Fact]
    public async Task SetUserActive_ForAnUnknownUser_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.SetUserActiveAsync(Guid.NewGuid(), false));
    }

    // ---- Password reset ----

    [Fact]
    public async Task ResetPassword_ReplacesTheOldPasswordWithTheNewOne()
    {
        var id = await Create();

        var result = await _service.ResetPasswordAsync(id, "BrandNewPass1");

        Assert.True(result.Succeeded);
        Assert.Null(await _service.ValidateCredentialsAsync("mariam@codecamp.demo", "DemoPass123"));
        Assert.NotNull(await _service.ValidateCredentialsAsync("mariam@codecamp.demo", "BrandNewPass1"));
    }

    [Fact]
    public async Task ResetPassword_WithAPasswordThePolicyRejects_FailsWithoutLockingTheUserOut()
    {
        // Regression: the old password used to be removed *before* the new one was validated, so a rejected
        // new password left the account with no password at all.
        var id = await Create();

        var result = await _service.ResetPasswordAsync(id, "short");

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
        Assert.NotNull(await _service.ValidateCredentialsAsync("mariam@codecamp.demo", "DemoPass123"));
    }

    [Fact]
    public async Task ResetPassword_ForAnUnknownUser_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ResetPasswordAsync(Guid.NewGuid(), "BrandNewPass1"));
    }

    // ---- Reads ----

    [Fact]
    public async Task AnyUsersExist_FlipsOnceTheFirstUserIsCreated()
    {
        Assert.False(await _service.AnyUsersExistAsync());

        await Create();

        Assert.True(await _service.AnyUsersExistAsync());
    }

    [Fact]
    public async Task UsersInRole_AreFilteredByRole_AndSortedByName()
    {
        await Create("zaki@codecamp.demo", role: RoleNames.Teacher, name: "Zaki");
        await Create("amr@codecamp.demo", role: RoleNames.Teacher, name: "Amr");
        await Create("desk@codecamp.demo", role: RoleNames.FrontDesk, name: "Desk");

        var teachers = await _service.GetUsersInRoleAsync(RoleNames.Teacher);
        var everyone = await _service.GetStaffUsersAsync();

        Assert.Equal(["Amr", "Zaki"], teachers.Select(t => t.FullName).ToArray());
        Assert.Equal(["Amr", "Desk", "Zaki"], everyone.Select(t => t.FullName).ToArray());
    }

    [Fact]
    public async Task GetAuthenticatedUser_ForAnUnknownId_ThrowsInvalidOperation()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetAuthenticatedUserAsync(Guid.NewGuid()));
    }
}

using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public IdentityService(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public async Task<CreateUserResult> CreateUserAsync(string email, string password, string fullName, string phoneNumber, string roleName)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            PhoneNumber = phoneNumber
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return new CreateUserResult(false, Guid.Empty, result.Errors.Select(e => e.Description).ToList());
        }

        await _userManager.AddToRoleAsync(user, roleName);

        return new CreateUserResult(true, user.Id, Array.Empty<string>());
    }

    public async Task<AuthenticatedUser?> ValidateCredentialsAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!isPasswordValid)
        {
            return null;
        }

        return await BuildAuthenticatedUserAsync(user);
    }

    public async Task<AuthenticatedUser> GetAuthenticatedUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"User '{userId}' was not found.");

        return await BuildAuthenticatedUserAsync(user);
    }

    public async Task<List<AuthenticatedUser>> GetStaffUsersAsync()
    {
        var users = await _context.Users
            .OrderBy(u => u.FullName)
            .ToListAsync();

        var result = new List<AuthenticatedUser>();
        foreach (var user in users)
        {
            result.Add(await BuildAuthenticatedUserAsync(user));
        }

        return result;
    }

    public async Task<List<AuthenticatedUser>> GetUsersInRoleAsync(string roleName)
    {
        var users = await _userManager.GetUsersInRoleAsync(roleName);

        var result = new List<AuthenticatedUser>();
        foreach (var user in users.OrderBy(u => u.FullName))
        {
            result.Add(await BuildAuthenticatedUserAsync(user));
        }

        return result;
    }

    public async Task SetUserActiveAsync(Guid userId, bool isActive)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException(nameof(ApplicationUser), userId);

        user.IsActive = isActive;
        await _userManager.UpdateAsync(user);
    }

    public Task<bool> AnyUsersExistAsync() => _context.Users.AnyAsync();

    private async Task<AuthenticatedUser> BuildAuthenticatedUserAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var branchIds = await _context.UserBranchAssignments
            .Where(a => a.UserId == user.Id)
            .Select(a => a.BranchId)
            .ToListAsync();

        return new AuthenticatedUser(user.Id, user.Email!, user.FullName, roles.ToList(), branchIds, user.IsActive);
    }
}

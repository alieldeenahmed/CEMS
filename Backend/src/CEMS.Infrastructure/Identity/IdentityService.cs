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

        // Creating the user and giving them their role are two writes; they must succeed or fail together,
        // or a failed role assignment would leave an account that can log in but has no role. If the caller
        // already opened a transaction (it shares this DbContext), that one is used instead.
        await using var ownTransaction = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync()
            : null;

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return new CreateUserResult(false, Guid.Empty, result.Errors.Select(e => e.Description).ToList());
        }

        var roleResult = await _userManager.AddToRoleAsync(user, roleName);
        if (!roleResult.Succeeded)
        {
            // Not committing rolls the user back too.
            return new CreateUserResult(false, Guid.Empty, roleResult.Errors.Select(e => e.Description).ToList());
        }

        if (ownTransaction is not null)
        {
            await ownTransaction.CommitAsync();
        }

        return new CreateUserResult(true, user.Id, Array.Empty<string>());
    }

    public async Task<AuthenticatedUser?> ValidateCredentialsAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        // Repeated wrong passwords lock the account for a while (see the Lockout options in
        // DependencyInjection). A locked account answers exactly like a wrong password, so an attacker
        // learns nothing extra -- not even that the lock has kicked in.
        if (await _userManager.IsLockedOutAsync(user))
        {
            return null;
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!isPasswordValid)
        {
            await _userManager.AccessFailedAsync(user);
            return null;
        }

        await _userManager.ResetAccessFailedCountAsync(user);
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
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not update user '{userId}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task<ResetPasswordResult> ResetPasswordAsync(Guid userId, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException(nameof(ApplicationUser), userId);

        // Validate the new password against the policy *before* touching the old one. Identity's
        // AddPassword validates too, but by then RemovePassword has already run, so a rejected new
        // password would leave the account with no password at all.
        var policyErrors = new List<string>();
        foreach (var validator in _userManager.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(_userManager, user, newPassword);
            if (!validation.Succeeded)
            {
                policyErrors.AddRange(validation.Errors.Select(e => e.Description));
            }
        }

        if (policyErrors.Count > 0)
        {
            return new ResetPasswordResult(false, policyErrors);
        }

        // Removing the old password and adding the new one are two writes; if the second failed after the
        // first, the account would be left with no password at all.
        await using var ownTransaction = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync()
            : null;

        var removeResult = await _userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
        {
            return new ResetPasswordResult(false, removeResult.Errors.Select(e => e.Description).ToList());
        }

        var addResult = await _userManager.AddPasswordAsync(user, newPassword);
        if (!addResult.Succeeded)
        {
            return new ResetPasswordResult(false, addResult.Errors.Select(e => e.Description).ToList());
        }

        if (ownTransaction is not null)
        {
            await ownTransaction.CommitAsync();
        }

        return new ResetPasswordResult(true, Array.Empty<string>());
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

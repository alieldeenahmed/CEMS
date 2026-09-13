namespace CEMS.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<CreateUserResult> CreateUserAsync(string email, string password, string fullName, string phoneNumber, string roleName);

    Task<AuthenticatedUser?> ValidateCredentialsAsync(string email, string password);

    Task<AuthenticatedUser> GetAuthenticatedUserAsync(Guid userId);

    Task<List<AuthenticatedUser>> GetStaffUsersAsync();

    Task SetUserActiveAsync(Guid userId, bool isActive);
}

public record CreateUserResult(bool Succeeded, Guid UserId, IReadOnlyList<string> Errors);

public record AuthenticatedUser(Guid UserId, string Email, string FullName, IReadOnlyList<string> Roles, IReadOnlyList<Guid> BranchIds, bool IsActive);

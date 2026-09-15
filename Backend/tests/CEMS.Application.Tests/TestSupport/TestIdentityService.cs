using CEMS.Application.Common.Interfaces;

namespace CEMS.Application.Tests.TestSupport;

/// <summary>
/// A lightweight fake for handlers that only need to read/act on user identity (roles, branches),
/// not the real password-hashing/ASP.NET Identity plumbing.
/// </summary>
public class TestIdentityService : IIdentityService
{
    private readonly Dictionary<Guid, AuthenticatedUser> _users = new();
    public List<(Guid UserId, string NewPassword)> ResetPasswordCalls { get; } = new();

    public void AddUser(AuthenticatedUser user) => _users[user.UserId] = user;

    public Task<AuthenticatedUser> GetAuthenticatedUserAsync(Guid userId) =>
        Task.FromResult(_users.TryGetValue(userId, out var user) ? user : throw new KeyNotFoundException($"No test user seeded for {userId}"));

    public Task<ResetPasswordResult> ResetPasswordAsync(Guid userId, string newPassword)
    {
        ResetPasswordCalls.Add((userId, newPassword));
        return Task.FromResult(new ResetPasswordResult(true, Array.Empty<string>()));
    }

    public Task<CreateUserResult> CreateUserAsync(string email, string password, string fullName, string phoneNumber, string roleName) =>
        throw new NotImplementedException();

    public Task<AuthenticatedUser?> ValidateCredentialsAsync(string email, string password) =>
        throw new NotImplementedException();

    public Task<List<AuthenticatedUser>> GetStaffUsersAsync() => Task.FromResult(_users.Values.ToList());

    public Task<List<AuthenticatedUser>> GetUsersInRoleAsync(string roleName) =>
        Task.FromResult(_users.Values.Where(u => u.Roles.Contains(roleName)).ToList());

    public Task SetUserActiveAsync(Guid userId, bool isActive) => Task.CompletedTask;

    public Task<bool> AnyUsersExistAsync() => Task.FromResult(_users.Count > 0);
}

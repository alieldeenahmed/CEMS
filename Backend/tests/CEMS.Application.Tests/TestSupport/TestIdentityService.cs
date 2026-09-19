using CEMS.Application.Common.Interfaces;

namespace CEMS.Application.Tests.TestSupport;

/// <summary>
/// A lightweight fake for handlers that only need to read/act on user identity (roles, branches),
/// not the real password-hashing/ASP.NET Identity plumbing.
/// </summary>
public class TestIdentityService : IIdentityService
{
    private readonly Dictionary<Guid, AuthenticatedUser> _users = new();
    private readonly Dictionary<string, (string Password, Guid UserId)> _credentials = new(StringComparer.OrdinalIgnoreCase);

    public List<(Guid UserId, string NewPassword)> ResetPasswordCalls { get; } = new();
    public List<(string Email, string Role)> CreatedUsers { get; } = new();
    public List<(Guid UserId, bool IsActive)> SetActiveCalls { get; } = new();

    /// <summary>When set, CreateUserAsync fails with these errors (e.g. a duplicate email).</summary>
    public IReadOnlyList<string>? CreateUserErrors { get; set; }

    public void AddUser(AuthenticatedUser user) => _users[user.UserId] = user;

    public void AddUser(AuthenticatedUser user, string password)
    {
        _users[user.UserId] = user;
        _credentials[user.Email] = (password, user.UserId);
    }

    public Task<AuthenticatedUser> GetAuthenticatedUserAsync(Guid userId) =>
        Task.FromResult(_users.TryGetValue(userId, out var user) ? user : throw new InvalidOperationException($"User '{userId}' was not found."));

    public Task<ResetPasswordResult> ResetPasswordAsync(Guid userId, string newPassword)
    {
        ResetPasswordCalls.Add((userId, newPassword));
        return Task.FromResult(new ResetPasswordResult(true, Array.Empty<string>()));
    }

    public Task<CreateUserResult> CreateUserAsync(string email, string password, string fullName, string phoneNumber, string roleName)
    {
        if (CreateUserErrors is not null)
        {
            return Task.FromResult(new CreateUserResult(false, Guid.Empty, CreateUserErrors));
        }

        var user = new AuthenticatedUser(Guid.NewGuid(), email, fullName, [roleName], Array.Empty<Guid>(), true);
        AddUser(user, password);
        CreatedUsers.Add((email, roleName));
        return Task.FromResult(new CreateUserResult(true, user.UserId, Array.Empty<string>()));
    }

    public Task<AuthenticatedUser?> ValidateCredentialsAsync(string email, string password)
    {
        if (_credentials.TryGetValue(email, out var entry) && entry.Password == password)
        {
            return Task.FromResult<AuthenticatedUser?>(_users[entry.UserId]);
        }

        return Task.FromResult<AuthenticatedUser?>(null);
    }

    public Task<List<AuthenticatedUser>> GetStaffUsersAsync() => Task.FromResult(_users.Values.ToList());

    public Task<List<AuthenticatedUser>> GetUsersInRoleAsync(string roleName) =>
        Task.FromResult(_users.Values.Where(u => u.Roles.Contains(roleName)).ToList());

    public Task SetUserActiveAsync(Guid userId, bool isActive)
    {
        SetActiveCalls.Add((userId, isActive));
        return Task.CompletedTask;
    }

    public Task<bool> AnyUsersExistAsync() => Task.FromResult(_users.Count > 0);
}

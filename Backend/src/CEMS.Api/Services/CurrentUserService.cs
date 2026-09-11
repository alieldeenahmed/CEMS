using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Users;

namespace CEMS.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var value = User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? new List<string>();

    public IReadOnlyList<Guid> BranchIds =>
        User?.FindAll("branch_id").Select(c => Guid.Parse(c.Value)).ToList() ?? new List<Guid>();

    public bool IsInRole(string role) => Roles.Contains(role);

    public bool HasAccessToBranch(Guid branchId) => IsInRole(RoleNames.Owner) || BranchIds.Contains(branchId);
}

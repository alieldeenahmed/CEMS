using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.TestSupport;

public class TestCurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<Guid> BranchIds { get; set; } = new();

    IReadOnlyList<string> ICurrentUserService.Roles => Roles;
    IReadOnlyList<Guid> ICurrentUserService.BranchIds => BranchIds;

    public bool IsInRole(string role) => Roles.Contains(role);

    public bool HasAccessToBranch(Guid branchId) => IsInRole(RoleNames.Owner) || BranchIds.Contains(branchId);
}

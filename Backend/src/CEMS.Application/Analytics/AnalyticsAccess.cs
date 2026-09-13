using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Users;

namespace CEMS.Application.Analytics;

/// <summary>
/// Every analytics query shares the same access rule: Owner can query any branch or omit
/// BranchId for an org-wide view; BranchManager must supply a BranchId they actually have
/// access to (omitting it would otherwise default to an implicit org-wide view, which a
/// branch-scoped role must never get).
/// </summary>
public static class AnalyticsAccess
{
    public static void EnsureAccess(ICurrentUserService currentUser, Guid? branchId)
    {
        if (currentUser.IsInRole(RoleNames.Owner))
        {
            return;
        }

        if (!branchId.HasValue)
        {
            throw new ForbiddenAccessException("BranchId is required for non-Owner users.");
        }

        if (!currentUser.HasAccessToBranch(branchId.Value))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }
    }
}

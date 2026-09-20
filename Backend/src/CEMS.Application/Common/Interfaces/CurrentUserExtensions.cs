using CEMS.Application.Common.Exceptions;

namespace CEMS.Application.Common.Interfaces;

public static class CurrentUserExtensions
{
    /// <summary>
    /// The branch-scope check every handler that touches branch-owned data makes, in one place so the rule and its
    /// error cannot drift apart between handlers. Call it as soon as the record's branch is known, before anything
    /// about the record is returned or changed.
    /// </summary>
    public static void EnsureAccessToBranch(this ICurrentUserService currentUser, Guid branchId)
    {
        if (!currentUser.HasAccessToBranch(branchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }
    }
}

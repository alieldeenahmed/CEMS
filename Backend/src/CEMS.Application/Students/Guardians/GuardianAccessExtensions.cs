using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Students;
using CEMS.Domain.Users;

namespace CEMS.Application.Students.Guardians;

public static class GuardianAccessExtensions
{
    /// <summary>
    /// Guardians aren't owned by a branch directly; they're reachable through their students. An Owner
    /// sees every guardian. Anyone else sees a guardian only if at least one of that guardian's students
    /// is currently at one of the caller's branches. A guardian with no students yet is also visible,
    /// so a guardian created a moment before being linked to a student isn't lost to the person who made it.
    /// </summary>
    public static IQueryable<Guardian> VisibleTo(this IQueryable<Guardian> guardians, ICurrentUserService currentUser)
    {
        if (currentUser.IsInRole(RoleNames.Owner))
        {
            return guardians;
        }

        var branchIds = currentUser.BranchIds;

        return guardians.Where(g =>
            !g.StudentGuardians.Any()
            || g.StudentGuardians.Any(sg => branchIds.Contains(sg.Student.CurrentBranchId)));
    }
}

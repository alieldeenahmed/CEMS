using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;

namespace CEMS.Application.Teachers;

/// <summary>PayType and PayRate are null when the viewer is not entitled to see what a teacher is paid.</summary>
public record TeacherDto(Guid Id, Guid UserId, string FullName, string Email, DateOnly HireDate, PayType? PayType, decimal? PayRate, IReadOnlyList<Guid> BranchIds)
{
    /// <summary>
    /// Pay is visible to the Owner, to Branch Managers (who set it when they create a teacher), and to the teacher
    /// themself -- but not to the front desk, who only need to know who teaches where.
    /// </summary>
    public static bool CanSeePay(ICurrentUserService viewer, Guid teacherUserId) =>
        viewer.IsInRole(RoleNames.Owner) || viewer.IsInRole(RoleNames.BranchManager) || viewer.UserId == teacherUserId;
}

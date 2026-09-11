namespace CEMS.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<Guid> BranchIds { get; }

    bool IsInRole(string role);

    bool HasAccessToBranch(Guid branchId);
}

using CEMS.Domain.Branches;

namespace CEMS.Domain.Users;

public class UserBranchAssignment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
}

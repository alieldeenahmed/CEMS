using CEMS.Domain.Branches;

namespace CEMS.Domain.Students;

public class StudentBranchHistory
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public Guid FromBranchId { get; set; }
    public Branch FromBranch { get; set; } = null!;

    public Guid ToBranchId { get; set; }
    public Branch ToBranch { get; set; } = null!;

    public DateOnly TransferDate { get; set; }
    public string? Reason { get; set; }
    public Guid? TransferredByUserId { get; set; }
}

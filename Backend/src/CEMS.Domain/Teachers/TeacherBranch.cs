using CEMS.Domain.Branches;

namespace CEMS.Domain.Teachers;

public class TeacherBranch
{
    public Guid TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
}

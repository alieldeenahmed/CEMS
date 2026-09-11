using CEMS.Domain.Branches;

namespace CEMS.Domain.Students;

public class Student
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public DateOnly EnrollmentDate { get; set; }
    public StudentStatus Status { get; set; } = StudentStatus.Active;

    public Guid CurrentBranchId { get; set; }
    public Branch CurrentBranch { get; set; } = null!;

    public ICollection<StudentGuardian> StudentGuardians { get; set; } = new List<StudentGuardian>();
}

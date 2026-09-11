namespace CEMS.Domain.Students;

public class StudentGuardian
{
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public Guid GuardianId { get; set; }
    public Guardian Guardian { get; set; } = null!;

    public RelationshipType RelationshipType { get; set; }
    public bool IsPrimaryContact { get; set; }
}

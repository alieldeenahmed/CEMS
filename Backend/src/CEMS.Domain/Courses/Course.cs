using CEMS.Domain.Branches;

namespace CEMS.Domain.Courses;

public class Course
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DeliveryMode DeliveryMode { get; set; }

    public Guid CurriculumId { get; set; }
    public Curriculum Curriculum { get; set; } = null!;

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public ICollection<CourseEnrollment> Enrollments { get; set; } = new List<CourseEnrollment>();
}

using CEMS.Domain.Students;

namespace CEMS.Domain.Courses;

public class CourseEnrollment
{
    public Guid Id { get; set; }
    public DateOnly EnrollmentDate { get; set; }
    public CourseEnrollmentStatus Status { get; set; } = CourseEnrollmentStatus.Active;
    public int? Position { get; set; }

    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
}

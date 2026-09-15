using CEMS.Domain.Courses;

namespace CEMS.Domain.Teachers;

/// <summary>
/// Declares that a teacher is qualified to teach a course -- distinct from CourseSession.TeacherId,
/// which tracks who is actually scheduled. Nothing in scheduling currently reads this; it's a
/// standalone record for staffing decisions (e.g. picking a substitute) until something needs it.
/// </summary>
public class TeacherCourseQualification
{
    public Guid TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
}

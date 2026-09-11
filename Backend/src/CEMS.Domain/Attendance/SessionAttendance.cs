using CEMS.Domain.Courses;
using CEMS.Domain.Students;

namespace CEMS.Domain.Attendance;

public class SessionAttendance
{
    public Guid Id { get; set; }

    public Guid CourseSessionId { get; set; }
    public CourseSession CourseSession { get; set; } = null!;

    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Unmarked;
    public DateTime? MarkedAtUtc { get; set; }
    public Guid? MarkedByUserId { get; set; }
}

using CEMS.Domain.Branches;
using CEMS.Domain.Teachers;

namespace CEMS.Domain.Courses;

public class CourseSession
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public Guid TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;

    public bool Overridden { get; set; }
    public string? OverrideReason { get; set; }
    public Guid? OverriddenByUserId { get; set; }

    public Guid? RescheduledToSessionId { get; set; }
    public CourseSession? RescheduledToSession { get; set; }
}

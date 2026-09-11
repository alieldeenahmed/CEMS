using CEMS.Domain.Attendance;

namespace CEMS.Application.Attendance;

public record AttendanceRecordDto(
    Guid? Id,
    Guid CourseSessionId,
    Guid StudentId,
    AttendanceStatus Status,
    DateTime? MarkedAtUtc,
    Guid? MarkedByUserId);

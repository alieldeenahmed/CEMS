using CEMS.Domain.Attendance;

namespace CEMS.Application.Attendance;

public record AttendanceRecordDto(
    Guid? Id,
    Guid CourseSessionId,
    Guid StudentId,
    string StudentFullName,
    AttendanceStatus Status,
    DateTime? MarkedAtUtc,
    Guid? MarkedByUserId);

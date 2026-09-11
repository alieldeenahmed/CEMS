using CEMS.Domain.Courses;

namespace CEMS.Application.Scheduling;

public record CourseSessionDto(
    Guid Id,
    Guid CourseId,
    Guid RoomId,
    Guid TeacherId,
    DateTime StartUtc,
    DateTime EndUtc,
    SessionStatus Status,
    bool Overridden,
    string? OverrideReason,
    Guid? RescheduledToSessionId);

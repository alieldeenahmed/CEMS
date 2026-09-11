using MediatR;

namespace CEMS.Application.Attendance.Queries.GetAttendanceForSession;

public record GetAttendanceForSessionQuery(Guid SessionId) : IRequest<List<AttendanceRecordDto>>;

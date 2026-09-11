using CEMS.Domain.Attendance;
using MediatR;

namespace CEMS.Application.Attendance.Commands.MarkAttendance;

public record MarkAttendanceCommand(Guid SessionId, Guid StudentId, AttendanceStatus Status) : IRequest<AttendanceRecordDto>;

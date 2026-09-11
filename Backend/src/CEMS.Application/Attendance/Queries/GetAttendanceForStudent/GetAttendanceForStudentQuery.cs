using MediatR;

namespace CEMS.Application.Attendance.Queries.GetAttendanceForStudent;

public record GetAttendanceForStudentQuery(Guid StudentId) : IRequest<List<AttendanceRecordDto>>;

using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Attendance;
using CEMS.Domain.Courses;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Attendance.Queries.GetAttendanceForSession;

public class GetAttendanceForSessionQueryHandler : IRequestHandler<GetAttendanceForSessionQuery, List<AttendanceRecordDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAttendanceForSessionQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<AttendanceRecordDto>> Handle(GetAttendanceForSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await _context.CourseSessions
            .Include(s => s.Course)
            .Include(s => s.Teacher)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(CourseSession), request.SessionId);

        var isOwnSession = session.Teacher.UserId == _currentUser.UserId;
        var hasAccess = _currentUser.IsInRole(RoleNames.Owner) || isOwnSession || _currentUser.HasAccessToBranch(session.Course.BranchId);

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this session.");
        }

        var enrolledStudentIds = await _context.CourseEnrollments
            .Where(e => e.CourseId == session.CourseId && e.Status == CourseEnrollmentStatus.Active)
            .Select(e => e.StudentId)
            .ToListAsync(cancellationToken);

        var existingRecords = await _context.SessionAttendances
            .Where(a => a.CourseSessionId == request.SessionId)
            .ToDictionaryAsync(a => a.StudentId, cancellationToken);

        return enrolledStudentIds
            .OrderBy(id => id)
            .Select(studentId => existingRecords.TryGetValue(studentId, out var record)
                ? new AttendanceRecordDto(record.Id, record.CourseSessionId, record.StudentId, record.Status, record.MarkedAtUtc, record.MarkedByUserId)
                : new AttendanceRecordDto(null, request.SessionId, studentId, AttendanceStatus.Unmarked, null, null))
            .ToList();
    }
}

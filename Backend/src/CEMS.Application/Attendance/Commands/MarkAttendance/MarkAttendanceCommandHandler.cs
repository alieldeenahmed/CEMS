using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Attendance;
using CEMS.Domain.Courses;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Attendance.Commands.MarkAttendance;

public class MarkAttendanceCommandHandler : IRequestHandler<MarkAttendanceCommand, AttendanceRecordDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public MarkAttendanceCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<AttendanceRecordDto> Handle(MarkAttendanceCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.CourseSessions
            .Include(s => s.Course)
            .Include(s => s.Teacher)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(CourseSession), request.SessionId);

        var hasAccess = _currentUser.IsInRole(RoleNames.Owner)
            || (_currentUser.IsInRole(RoleNames.BranchManager) && _currentUser.HasAccessToBranch(session.Course.BranchId))
            || session.Teacher.UserId == _currentUser.UserId;

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to mark attendance for this session.");
        }

        if (session.Status == SessionStatus.Cancelled)
        {
            throw new BadRequestException(new[] { "Cannot mark attendance for a cancelled session." });
        }

        if (session.StartUtc > DateTime.UtcNow)
        {
            throw new BadRequestException(new[] { "Cannot mark attendance for a session that hasn't started yet." });
        }

        if (DateTime.UtcNow > session.EndUtc.AddHours(4))
        {
            throw new BadRequestException(new[] { "The attendance window for this session has closed. Attendance can only be marked or edited within 4 hours of the session ending." });
        }

        var studentFullName = await _context.CourseEnrollments
            .Where(e => e.StudentId == request.StudentId && e.CourseId == session.CourseId && e.Status == CourseEnrollmentStatus.Active)
            .Select(e => e.Student.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        if (studentFullName is null)
        {
            throw new BadRequestException(new[] { "This student is not actively enrolled in this session's course." });
        }

        var attendance = await _context.SessionAttendances.FirstOrDefaultAsync(
            a => a.CourseSessionId == request.SessionId && a.StudentId == request.StudentId,
            cancellationToken);

        if (attendance is null)
        {
            attendance = new SessionAttendance
            {
                Id = Guid.NewGuid(),
                CourseSessionId = request.SessionId,
                StudentId = request.StudentId
            };
            _context.SessionAttendances.Add(attendance);
        }

        attendance.Status = request.Status;
        attendance.MarkedAtUtc = DateTime.UtcNow;
        attendance.MarkedByUserId = _currentUser.UserId;

        await _context.SaveChangesAsync(cancellationToken);

        return new AttendanceRecordDto(
            attendance.Id,
            attendance.CourseSessionId,
            session.CourseId,
            session.Course.Name,
            session.StartUtc,
            attendance.StudentId,
            studentFullName,
            attendance.Status,
            attendance.MarkedAtUtc,
            attendance.MarkedByUserId);
    }
}

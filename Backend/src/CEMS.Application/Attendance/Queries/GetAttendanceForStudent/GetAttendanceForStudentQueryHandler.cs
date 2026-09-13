using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Students;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Attendance.Queries.GetAttendanceForStudent;

public class GetAttendanceForStudentQueryHandler : IRequestHandler<GetAttendanceForStudentQuery, List<AttendanceRecordDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAttendanceForStudentQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<AttendanceRecordDto>> Handle(GetAttendanceForStudentQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        var hasAccess = _currentUser.IsInRole(RoleNames.Owner) || _currentUser.HasAccessToBranch(student.CurrentBranchId);

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this student.");
        }

        return await _context.SessionAttendances
            .Where(a => a.StudentId == request.StudentId)
            .OrderByDescending(a => a.CourseSession.StartUtc)
            .Select(a => new AttendanceRecordDto(a.Id, a.CourseSessionId, a.StudentId, student.FullName, a.Status, a.MarkedAtUtc, a.MarkedByUserId))
            .ToListAsync(cancellationToken);
    }
}

using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Students;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Queries.GetEnrollmentsForStudent;

public class GetEnrollmentsForStudentQueryHandler : IRequestHandler<GetEnrollmentsForStudentQuery, List<CourseEnrollmentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetEnrollmentsForStudentQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<CourseEnrollmentDto>> Handle(GetEnrollmentsForStudentQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        var isFullAccess = _currentUser.IsInRole(RoleNames.Owner) || _currentUser.HasAccessToBranch(student.CurrentBranchId);

        var teacherCourseIds = _context.CourseSessions
            .Where(s => s.Teacher.UserId == _currentUser.UserId)
            .Select(s => s.CourseId);

        var isTeachingStudent = _currentUser.IsInRole(RoleNames.Teacher)
            && await _context.CourseEnrollments.AnyAsync(e => e.StudentId == request.StudentId && teacherCourseIds.Contains(e.CourseId), cancellationToken);

        if (!isFullAccess && !isTeachingStudent)
        {
            throw new ForbiddenAccessException("You do not have access to this student.");
        }

        var query = _context.CourseEnrollments.Where(e => e.StudentId == request.StudentId);

        if (!isFullAccess)
        {
            query = query.Where(e => teacherCourseIds.Contains(e.CourseId));
        }

        return await query
            .OrderBy(e => e.EnrollmentDate)
            .Select(e => new CourseEnrollmentDto(e.Id, e.StudentId, e.CourseId, e.Course.Name, e.EnrollmentDate, e.Status, e.Position))
            .ToListAsync(cancellationToken);
    }
}

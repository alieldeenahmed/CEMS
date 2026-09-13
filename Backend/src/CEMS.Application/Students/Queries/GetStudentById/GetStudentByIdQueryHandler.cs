using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Students;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Students.Queries.GetStudentById;

public class GetStudentByIdQueryHandler : IRequestHandler<GetStudentByIdQuery, StudentDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetStudentByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<StudentDto> Handle(GetStudentByIdQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Student), request.Id);

        var hasAccess = _currentUser.IsInRole(RoleNames.Owner) || _currentUser.HasAccessToBranch(student.CurrentBranchId);

        if (!hasAccess && _currentUser.IsInRole(RoleNames.Teacher))
        {
            var teacherCourseIds = _context.CourseSessions
                .Where(s => s.Teacher.UserId == _currentUser.UserId)
                .Select(s => s.CourseId);

            hasAccess = await _context.CourseEnrollments
                .AnyAsync(e => e.StudentId == request.Id && teacherCourseIds.Contains(e.CourseId), cancellationToken);
        }

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this student.");
        }

        return StudentDto.FromEntity(student);
    }
}

using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Queries.GetEnrollmentsForCourse;

public class GetEnrollmentsForCourseQueryHandler : IRequestHandler<GetEnrollmentsForCourseQuery, List<CourseEnrollmentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetEnrollmentsForCourseQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<CourseEnrollmentDto>> Handle(GetEnrollmentsForCourseQuery request, CancellationToken cancellationToken)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Course), request.CourseId);

        if (!_currentUser.HasAccessToBranch(course.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this course.");
        }

        return await _context.CourseEnrollments
            .Where(e => e.CourseId == request.CourseId)
            .OrderBy(e => e.EnrollmentDate)
            .Select(e => new CourseEnrollmentDto(e.Id, e.StudentId, e.CourseId, e.EnrollmentDate, e.Status))
            .ToListAsync(cancellationToken);
    }
}

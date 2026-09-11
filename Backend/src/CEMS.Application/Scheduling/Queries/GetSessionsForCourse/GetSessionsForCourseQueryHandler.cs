using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Scheduling.Queries.GetSessionsForCourse;

public class GetSessionsForCourseQueryHandler : IRequestHandler<GetSessionsForCourseQuery, List<CourseSessionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetSessionsForCourseQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<CourseSessionDto>> Handle(GetSessionsForCourseQuery request, CancellationToken cancellationToken)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Course), request.CourseId);

        if (!_currentUser.HasAccessToBranch(course.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this course.");
        }

        return await _context.CourseSessions
            .Where(s => s.CourseId == request.CourseId)
            .OrderBy(s => s.StartUtc)
            .Select(s => new CourseSessionDto(
                s.Id, s.CourseId, s.RoomId, s.TeacherId, s.StartUtc, s.EndUtc,
                s.Status, s.Overridden, s.OverrideReason, s.RescheduledToSessionId))
            .ToListAsync(cancellationToken);
    }
}

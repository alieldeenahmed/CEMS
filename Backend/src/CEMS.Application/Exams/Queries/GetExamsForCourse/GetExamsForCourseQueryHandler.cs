using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Exams.Queries.GetExamsForCourse;

public class GetExamsForCourseQueryHandler : IRequestHandler<GetExamsForCourseQuery, List<ExamDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetExamsForCourseQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<ExamDto>> Handle(GetExamsForCourseQuery request, CancellationToken cancellationToken)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Course), request.CourseId);

        var hasAccess = _currentUser.HasAccessToBranch(course.BranchId)
            || await _context.CourseSessions.AnyAsync(s => s.CourseId == request.CourseId && s.Teacher.UserId == _currentUser.UserId, cancellationToken);

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this course.");
        }

        return await _context.Exams
            .Where(e => e.CourseId == request.CourseId)
            .OrderBy(e => e.ExamDate)
            .Select(e => new ExamDto(e.Id, e.CourseId, e.Name, e.MaxScore, e.ExamDate))
            .ToListAsync(cancellationToken);
    }
}

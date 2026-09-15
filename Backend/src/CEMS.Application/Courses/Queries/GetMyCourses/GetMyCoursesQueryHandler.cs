using CEMS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Queries.GetMyCourses;

public class GetMyCoursesQueryHandler : IRequestHandler<GetMyCoursesQuery, List<CourseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyCoursesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<CourseDto>> Handle(GetMyCoursesQuery request, CancellationToken cancellationToken)
    {
        return await _context.CourseSessions
            .Where(s => s.Teacher.UserId == _currentUser.UserId)
            .Select(s => s.Course)
            .Distinct()
            .OrderBy(c => c.Name)
            .Select(c => new CourseDto(c.Id, c.Name, c.DeliveryMode, c.CurriculumId, c.BranchId))
            .ToListAsync(cancellationToken);
    }
}

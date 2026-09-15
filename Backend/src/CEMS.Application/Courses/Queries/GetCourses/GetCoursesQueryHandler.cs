using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Queries.GetCourses;

public class GetCoursesQueryHandler : IRequestHandler<GetCoursesQuery, List<CourseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetCoursesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<CourseDto>> Handle(GetCoursesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Course> query = _context.Courses;

        if (!_currentUser.IsInRole(RoleNames.Owner))
        {
            var branchIds = _currentUser.BranchIds;
            query = query.Where(c => branchIds.Contains(c.BranchId));
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new CourseDto(c.Id, c.Name, c.DeliveryMode, c.CurriculumId, c.BranchId))
            .ToListAsync(cancellationToken);
    }
}

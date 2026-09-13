using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payments.Queries.GetPackagesForCourse;

public class GetPackagesForCourseQueryHandler : IRequestHandler<GetPackagesForCourseQuery, List<PackageDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetPackagesForCourseQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<PackageDto>> Handle(GetPackagesForCourseQuery request, CancellationToken cancellationToken)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Course), request.CourseId);

        if (!_currentUser.HasAccessToBranch(course.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this course.");
        }

        return await _context.Packages
            .Where(p => p.CourseId == request.CourseId)
            .OrderBy(p => p.SessionCount)
            .Select(p => new PackageDto(p.Id, p.CourseId, p.SessionCount, p.Price))
            .ToListAsync(cancellationToken);
    }
}

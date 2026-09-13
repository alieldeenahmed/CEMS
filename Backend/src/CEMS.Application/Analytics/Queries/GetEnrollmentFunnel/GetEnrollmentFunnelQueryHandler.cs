using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Analytics.Queries.GetEnrollmentFunnel;

public class GetEnrollmentFunnelQueryHandler : IRequestHandler<GetEnrollmentFunnelQuery, EnrollmentFunnelDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetEnrollmentFunnelQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<EnrollmentFunnelDto> Handle(GetEnrollmentFunnelQuery request, CancellationToken cancellationToken)
    {
        AnalyticsAccess.EnsureAccess(_currentUser, request.BranchId);

        var students = _context.Students.AsQueryable();
        if (request.BranchId.HasValue)
        {
            students = students.Where(s => s.CurrentBranchId == request.BranchId.Value);
        }

        var totalStudents = await students.CountAsync(cancellationToken);

        var studentIds = await students.Select(s => s.Id).ToListAsync(cancellationToken);

        var studentsWithAnyEnrollment = await _context.CourseEnrollments
            .Where(e => studentIds.Contains(e.StudentId))
            .Select(e => e.StudentId)
            .Distinct()
            .CountAsync(cancellationToken);

        var studentsWithActiveEnrollment = await _context.CourseEnrollments
            .Where(e => studentIds.Contains(e.StudentId) && e.Status == CourseEnrollmentStatus.Active)
            .Select(e => e.StudentId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new EnrollmentFunnelDto(totalStudents, studentsWithAnyEnrollment, studentsWithActiveEnrollment);
    }
}

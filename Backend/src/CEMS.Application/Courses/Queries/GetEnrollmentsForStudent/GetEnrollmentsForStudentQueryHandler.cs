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

        var hasAccess = _currentUser.IsInRole(RoleNames.Owner)
            || (_currentUser.IsInRole(RoleNames.Parent) && await _context.StudentGuardians
                .AnyAsync(sg => sg.StudentId == student.Id && sg.Guardian.UserId == _currentUser.UserId, cancellationToken))
            || _currentUser.HasAccessToBranch(student.CurrentBranchId);

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this student.");
        }

        return await _context.CourseEnrollments
            .Where(e => e.StudentId == request.StudentId)
            .OrderBy(e => e.EnrollmentDate)
            .Select(e => new CourseEnrollmentDto(e.Id, e.StudentId, e.CourseId, e.EnrollmentDate, e.Status, e.Position))
            .ToListAsync(cancellationToken);
    }
}

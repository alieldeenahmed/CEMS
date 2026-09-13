using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Commands.PromoteFromWaitlist;

public class PromoteFromWaitlistCommandHandler : IRequestHandler<PromoteFromWaitlistCommand, CourseEnrollmentDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public PromoteFromWaitlistCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CourseEnrollmentDto> Handle(PromoteFromWaitlistCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _context.CourseEnrollments
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(CourseEnrollment), request.EnrollmentId);

        if (!_currentUser.HasAccessToBranch(enrollment.Course.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        if (enrollment.Status != CourseEnrollmentStatus.Waitlisted)
        {
            throw new BadRequestException(new[] { "Only a waitlisted enrollment can be promoted." });
        }

        enrollment.Status = CourseEnrollmentStatus.Active;
        enrollment.Position = null;

        await _context.SaveChangesAsync(cancellationToken);

        return new CourseEnrollmentDto(enrollment.Id, enrollment.StudentId, enrollment.CourseId, enrollment.Course.Name, enrollment.EnrollmentDate, enrollment.Status, enrollment.Position);
    }
}

using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Commands.DropEnrollment;

public class DropEnrollmentCommandHandler : IRequestHandler<DropEnrollmentCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DropEnrollmentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(DropEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _context.CourseEnrollments
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CourseEnrollment), request.Id);

        _currentUser.EnsureAccessToBranch(enrollment.Course.BranchId);

        // A dropped enrollment is no longer in the queue, so it must not keep a waitlist position (positions
        // are unique among the waitlisted).
        enrollment.Status = CourseEnrollmentStatus.Dropped;
        enrollment.Position = null;
        await _context.SaveChangesAsync(cancellationToken);
    }
}

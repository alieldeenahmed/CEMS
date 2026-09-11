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

        if (!_currentUser.HasAccessToBranch(enrollment.Course.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        enrollment.Status = CourseEnrollmentStatus.Dropped;
        await _context.SaveChangesAsync(cancellationToken);
    }
}

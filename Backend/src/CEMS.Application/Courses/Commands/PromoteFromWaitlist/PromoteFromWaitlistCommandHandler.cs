using CEMS.Application.Common.Concurrency;
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
        var courseId = await _context.CourseEnrollments
            .Where(e => e.Id == request.EnrollmentId)
            .Select(e => (Guid?)e.CourseId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(CourseEnrollment), request.EnrollmentId);

        // Promotion consumes a seat, so it is serialized with enrolling, per course. The enrollment is read
        // only after the lock is held, so it reflects any request that finished first.
        await using var transaction = await _context.BeginLockedTransactionAsync(
            cancellationToken, LockKeys.CourseEnrollments(courseId));

        var enrollment = await _context.CourseEnrollments
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(CourseEnrollment), request.EnrollmentId);

        _currentUser.EnsureAccessToBranch(enrollment.Course.BranchId);

        if (enrollment.Status != CourseEnrollmentStatus.Waitlisted)
        {
            throw new BadRequestException(new[] { "Only a waitlisted enrollment can be promoted." });
        }

        // A waitlist exists because the course is full, so promoting someone is only valid once a seat has
        // opened up. Without this the capacity limit could be bypassed by promoting straight away.
        var capacity = await _context.CourseSessions
            .Where(s => s.CourseId == enrollment.CourseId && s.Status != SessionStatus.Cancelled)
            .OrderBy(s => s.StartUtc)
            .Select(s => (int?)s.Room.Capacity)
            .FirstOrDefaultAsync(cancellationToken);

        if (capacity.HasValue)
        {
            var activeCount = await _context.CourseEnrollments.CountAsync(
                e => e.CourseId == enrollment.CourseId && e.Status == CourseEnrollmentStatus.Active, cancellationToken);

            if (activeCount >= capacity.Value)
            {
                throw new BadRequestException(new[] { "The course is still full; drop an active enrollment before promoting someone from the waitlist." });
            }
        }

        enrollment.Status = CourseEnrollmentStatus.Active;
        enrollment.Position = null;

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CourseEnrollmentDto(enrollment.Id, enrollment.StudentId, enrollment.CourseId, enrollment.Course.Name, enrollment.EnrollmentDate, enrollment.Status, enrollment.Position);
    }
}

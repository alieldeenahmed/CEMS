using CEMS.Application.Common.Concurrency;
using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using CEMS.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Commands.EnrollStudent;

public class EnrollStudentCommandHandler : IRequestHandler<EnrollStudentCommand, CourseEnrollmentDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public EnrollStudentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CourseEnrollmentDto> Handle(EnrollStudentCommand request, CancellationToken cancellationToken)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Course), request.CourseId);

        _currentUser.EnsureAccessToBranch(course.BranchId);

        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        if (student.CurrentBranchId != course.BranchId)
        {
            throw new BadRequestException(new[] { "The student's branch does not match the course's branch." });
        }

        // Everything below is check-then-write against the course's enrollment list (already enrolled? is it
        // full? next waitlist position?). Two simultaneous requests would each see the same answers, so they
        // are serialized per course. (Unique indexes on the table are the backstop.)
        await using var transaction = await _context.BeginLockedTransactionAsync(
            cancellationToken, LockKeys.CourseEnrollments(request.CourseId));

        var alreadyEnrolled = await _context.CourseEnrollments.AnyAsync(
            e => e.StudentId == request.StudentId && e.CourseId == request.CourseId
                && (e.Status == CourseEnrollmentStatus.Active || e.Status == CourseEnrollmentStatus.Waitlisted),
            cancellationToken);

        if (alreadyEnrolled)
        {
            throw new BadRequestException(new[] { "This student is already enrolled or waitlisted for this course." });
        }

        // Capacity is derived from the room of the course's earliest scheduled session. A course
        // with no sessions yet has no known capacity, so enrollment is unconstrained until one exists.
        var capacity = await _context.CourseSessions
            .Where(s => s.CourseId == request.CourseId && s.Status != SessionStatus.Cancelled)
            .OrderBy(s => s.StartUtc)
            .Select(s => (int?)s.Room.Capacity)
            .FirstOrDefaultAsync(cancellationToken);

        var activeCount = await _context.CourseEnrollments.CountAsync(
            e => e.CourseId == request.CourseId && e.Status == CourseEnrollmentStatus.Active,
            cancellationToken);

        var isFull = capacity.HasValue && activeCount >= capacity.Value;

        var enrollment = new CourseEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            CourseId = request.CourseId,
            EnrollmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = isFull ? CourseEnrollmentStatus.Waitlisted : CourseEnrollmentStatus.Active
        };

        if (isFull)
        {
            var nextPosition = await _context.CourseEnrollments
                .Where(e => e.CourseId == request.CourseId && e.Status == CourseEnrollmentStatus.Waitlisted)
                .Select(e => e.Position)
                .DefaultIfEmpty()
                .MaxAsync(cancellationToken) ?? 0;

            enrollment.Position = nextPosition + 1;
        }

        _context.CourseEnrollments.Add(enrollment);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CourseEnrollmentDto(enrollment.Id, enrollment.StudentId, enrollment.CourseId, course.Name, enrollment.EnrollmentDate, enrollment.Status, enrollment.Position);
    }
}

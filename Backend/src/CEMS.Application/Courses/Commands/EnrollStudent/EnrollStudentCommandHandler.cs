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

        if (!_currentUser.HasAccessToBranch(course.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        if (student.CurrentBranchId != course.BranchId)
        {
            throw new BadRequestException(new[] { "The student's branch does not match the course's branch." });
        }

        var alreadyEnrolled = await _context.CourseEnrollments.AnyAsync(
            e => e.StudentId == request.StudentId && e.CourseId == request.CourseId && e.Status == CourseEnrollmentStatus.Active,
            cancellationToken);

        if (alreadyEnrolled)
        {
            throw new BadRequestException(new[] { "This student is already actively enrolled in this course." });
        }

        var enrollment = new CourseEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            CourseId = request.CourseId,
            EnrollmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = CourseEnrollmentStatus.Active
        };

        _context.CourseEnrollments.Add(enrollment);
        await _context.SaveChangesAsync(cancellationToken);

        return new CourseEnrollmentDto(enrollment.Id, enrollment.StudentId, enrollment.CourseId, enrollment.EnrollmentDate, enrollment.Status);
    }
}

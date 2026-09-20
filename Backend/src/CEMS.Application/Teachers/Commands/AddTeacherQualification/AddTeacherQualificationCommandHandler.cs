using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using CEMS.Domain.Teachers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Teachers.Commands.AddTeacherQualification;

public class AddTeacherQualificationCommandHandler : IRequestHandler<AddTeacherQualificationCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddTeacherQualificationCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(AddTeacherQualificationCommand request, CancellationToken cancellationToken)
    {
        var teacherExists = await _context.Teachers.AnyAsync(t => t.Id == request.TeacherId, cancellationToken);
        if (!teacherExists)
        {
            throw new NotFoundException(nameof(Teacher), request.TeacherId);
        }

        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Course), request.CourseId);

        _currentUser.EnsureAccessToBranch(course.BranchId);

        var alreadyQualified = await _context.TeacherCourseQualifications
            .AnyAsync(q => q.TeacherId == request.TeacherId && q.CourseId == request.CourseId, cancellationToken);

        if (alreadyQualified)
        {
            throw new BadRequestException(new[] { "This teacher is already declared qualified for this course." });
        }

        _context.TeacherCourseQualifications.Add(new TeacherCourseQualification { TeacherId = request.TeacherId, CourseId = request.CourseId });
        await _context.SaveChangesAsync(cancellationToken);
    }
}

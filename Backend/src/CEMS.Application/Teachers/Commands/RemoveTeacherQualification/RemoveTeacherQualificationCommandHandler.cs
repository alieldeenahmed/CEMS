using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Teachers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Teachers.Commands.RemoveTeacherQualification;

public class RemoveTeacherQualificationCommandHandler : IRequestHandler<RemoveTeacherQualificationCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RemoveTeacherQualificationCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(RemoveTeacherQualificationCommand request, CancellationToken cancellationToken)
    {
        var link = await _context.TeacherCourseQualifications
            .Include(q => q.Course)
            .FirstOrDefaultAsync(q => q.TeacherId == request.TeacherId && q.CourseId == request.CourseId, cancellationToken)
            ?? throw new NotFoundException(nameof(TeacherCourseQualification), $"{request.TeacherId}/{request.CourseId}");

        if (!_currentUser.HasAccessToBranch(link.Course.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        _context.TeacherCourseQualifications.Remove(link);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

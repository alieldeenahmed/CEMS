using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Commands.DeleteCourse;

public class DeleteCourseCommandHandler : IRequestHandler<DeleteCourseCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteCourseCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Course), request.Id);

        if (!_currentUser.HasAccessToBranch(course.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        _context.Courses.Remove(course);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new BadRequestException(new[] { "Cannot delete a course that still has enrollments." });
        }
    }
}

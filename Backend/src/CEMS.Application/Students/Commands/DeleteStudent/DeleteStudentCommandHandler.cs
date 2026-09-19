using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Students.Commands.DeleteStudent;

public class DeleteStudentCommandHandler : IRequestHandler<DeleteStudentCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteStudentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteStudentCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Student), request.Id);

        if (!_currentUser.HasAccessToBranch(student.CurrentBranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        // A student with enrollments, attendance, grades, or invoices has records that must survive them
        // (billing in particular), and the database refuses the delete anyway -- so say why, rather than
        // letting it surface as a 500. Such a student can be marked Paused or Graduated instead.
        var hasHistory = await _context.CourseEnrollments.AnyAsync(e => e.StudentId == request.Id, cancellationToken)
            || await _context.SessionAttendances.AnyAsync(a => a.StudentId == request.Id, cancellationToken)
            || await _context.Grades.AnyAsync(g => g.StudentId == request.Id, cancellationToken)
            || await _context.Invoices.AnyAsync(i => i.StudentId == request.Id, cancellationToken);

        if (hasHistory)
        {
            throw new BadRequestException(new[]
            {
                "This student has enrollments, attendance, grades, or invoices on record and can't be deleted. Set their status to Paused or Graduated instead."
            });
        }

        _context.Students.Remove(student);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

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

        _context.Students.Remove(student);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

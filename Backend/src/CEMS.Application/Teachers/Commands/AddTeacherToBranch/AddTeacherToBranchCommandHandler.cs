using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Branches;
using CEMS.Domain.Teachers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Teachers.Commands.AddTeacherToBranch;

public class AddTeacherToBranchCommandHandler : IRequestHandler<AddTeacherToBranchCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddTeacherToBranchCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(AddTeacherToBranchCommand request, CancellationToken cancellationToken)
    {
        var teacherExists = await _context.Teachers.AnyAsync(t => t.Id == request.TeacherId, cancellationToken);
        if (!teacherExists)
        {
            throw new NotFoundException(nameof(Teacher), request.TeacherId);
        }

        var branchExists = await _context.Branches.AnyAsync(b => b.Id == request.BranchId, cancellationToken);
        if (!branchExists)
        {
            throw new NotFoundException(nameof(Branch), request.BranchId);
        }

        if (!_currentUser.HasAccessToBranch(request.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        var alreadyAssigned = await _context.TeacherBranches
            .AnyAsync(tb => tb.TeacherId == request.TeacherId && tb.BranchId == request.BranchId, cancellationToken);

        if (alreadyAssigned)
        {
            throw new BadRequestException(new[] { "This teacher is already assigned to this branch." });
        }

        _context.TeacherBranches.Add(new TeacherBranch { TeacherId = request.TeacherId, BranchId = request.BranchId });
        await _context.SaveChangesAsync(cancellationToken);
    }
}

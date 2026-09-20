using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Teachers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Teachers.Commands.RemoveTeacherFromBranch;

public class RemoveTeacherFromBranchCommandHandler : IRequestHandler<RemoveTeacherFromBranchCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RemoveTeacherFromBranchCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(RemoveTeacherFromBranchCommand request, CancellationToken cancellationToken)
    {
        var link = await _context.TeacherBranches
            .FirstOrDefaultAsync(tb => tb.TeacherId == request.TeacherId && tb.BranchId == request.BranchId, cancellationToken)
            ?? throw new NotFoundException(nameof(TeacherBranch), $"{request.TeacherId}/{request.BranchId}");

        _currentUser.EnsureAccessToBranch(request.BranchId);

        _context.TeacherBranches.Remove(link);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

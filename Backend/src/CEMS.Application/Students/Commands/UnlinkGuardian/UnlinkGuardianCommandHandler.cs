using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Students.Commands.UnlinkGuardian;

public class UnlinkGuardianCommandHandler : IRequestHandler<UnlinkGuardianCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UnlinkGuardianCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(UnlinkGuardianCommand request, CancellationToken cancellationToken)
    {
        var link = await _context.StudentGuardians
            .Include(sg => sg.Student)
            .FirstOrDefaultAsync(sg => sg.StudentId == request.StudentId && sg.GuardianId == request.GuardianId, cancellationToken)
            ?? throw new NotFoundException(nameof(StudentGuardian), $"{request.StudentId}/{request.GuardianId}");

        _currentUser.EnsureAccessToBranch(link.Student.CurrentBranchId);

        _context.StudentGuardians.Remove(link);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

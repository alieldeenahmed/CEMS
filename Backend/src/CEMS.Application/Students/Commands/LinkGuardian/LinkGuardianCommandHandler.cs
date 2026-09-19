using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Application.Students.Guardians;
using CEMS.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Students.Commands.LinkGuardian;

public class LinkGuardianCommandHandler : IRequestHandler<LinkGuardianCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public LinkGuardianCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(LinkGuardianCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        if (!_currentUser.HasAccessToBranch(student.CurrentBranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        // A guardian the caller can't see is reported as not found, so linking can't be used to probe another branch's records.
        var guardianExists = await _context.Guardians.VisibleTo(_currentUser).AnyAsync(g => g.Id == request.GuardianId, cancellationToken);
        if (!guardianExists)
        {
            throw new NotFoundException(nameof(Guardian), request.GuardianId);
        }

        var alreadyLinked = await _context.StudentGuardians
            .AnyAsync(sg => sg.StudentId == request.StudentId && sg.GuardianId == request.GuardianId, cancellationToken);

        if (alreadyLinked)
        {
            throw new BadRequestException(new[] { "This guardian is already linked to this student." });
        }

        _context.StudentGuardians.Add(new StudentGuardian
        {
            StudentId = request.StudentId,
            GuardianId = request.GuardianId,
            RelationshipType = request.RelationshipType,
            IsPrimaryContact = request.IsPrimaryContact
        });

        await _context.SaveChangesAsync(cancellationToken);
    }
}

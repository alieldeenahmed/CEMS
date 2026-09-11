using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Teachers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Teachers.Commands.RemoveAvailability;

public class RemoveAvailabilityCommandHandler : IRequestHandler<RemoveAvailabilityCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RemoveAvailabilityCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(RemoveAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var availability = await _context.TeacherAvailabilities
            .Include(a => a.Teacher)
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TeacherAvailability), request.Id);

        var isOwnRecord = availability.Teacher.UserId == _currentUser.UserId;
        if (!isOwnRecord && !_currentUser.HasAccessToBranch(availability.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        _context.TeacherAvailabilities.Remove(availability);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

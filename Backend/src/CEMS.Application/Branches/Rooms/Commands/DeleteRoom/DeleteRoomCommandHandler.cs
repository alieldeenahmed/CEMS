using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Branches;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Branches.Rooms.Commands.DeleteRoom;

public class DeleteRoomCommandHandler : IRequestHandler<DeleteRoomCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteRoomCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Room), request.Id);

        _currentUser.EnsureAccessToBranch(room.BranchId);

        if (await _context.CourseSessions.AnyAsync(s => s.RoomId == request.Id, cancellationToken))
        {
            throw new BadRequestException(new[] { "Cannot delete a room that has sessions scheduled in it." });
        }

        _context.Rooms.Remove(room);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

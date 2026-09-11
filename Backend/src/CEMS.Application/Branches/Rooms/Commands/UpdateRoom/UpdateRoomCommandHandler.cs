using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Branches;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Branches.Rooms.Commands.UpdateRoom;

public class UpdateRoomCommandHandler : IRequestHandler<UpdateRoomCommand, RoomDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateRoomCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<RoomDto> Handle(UpdateRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Room), request.Id);

        if (!_currentUser.HasAccessToBranch(room.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        room.Name = request.Name;
        room.Capacity = request.Capacity;

        await _context.SaveChangesAsync(cancellationToken);

        return new RoomDto(room.Id, room.BranchId, room.Name, room.Capacity);
    }
}

using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Branches;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Branches.Rooms.Commands.CreateRoom;

public class CreateRoomCommandHandler : IRequestHandler<CreateRoomCommand, RoomDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateRoomCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<RoomDto> Handle(CreateRoomCommand request, CancellationToken cancellationToken)
    {
        var branchExists = await _context.Branches.AnyAsync(b => b.Id == request.BranchId, cancellationToken);
        if (!branchExists)
        {
            throw new NotFoundException(nameof(Branch), request.BranchId);
        }

        if (!_currentUser.HasAccessToBranch(request.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        var room = new Room
        {
            Id = Guid.NewGuid(),
            BranchId = request.BranchId,
            Name = request.Name,
            Capacity = request.Capacity
        };

        _context.Rooms.Add(room);
        await _context.SaveChangesAsync(cancellationToken);

        return new RoomDto(room.Id, room.BranchId, room.Name, room.Capacity);
    }
}

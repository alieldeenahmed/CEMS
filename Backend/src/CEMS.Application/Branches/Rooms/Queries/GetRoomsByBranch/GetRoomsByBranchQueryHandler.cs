using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Branches.Rooms.Queries.GetRoomsByBranch;

public class GetRoomsByBranchQueryHandler : IRequestHandler<GetRoomsByBranchQuery, List<RoomDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetRoomsByBranchQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<RoomDto>> Handle(GetRoomsByBranchQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasAccessToBranch(request.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        return await _context.Rooms
            .Where(r => r.BranchId == request.BranchId)
            .OrderBy(r => r.Name)
            .Select(r => new RoomDto(r.Id, r.BranchId, r.Name, r.Capacity))
            .ToListAsync(cancellationToken);
    }
}

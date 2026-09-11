using MediatR;

namespace CEMS.Application.Branches.Rooms.Queries.GetRoomsByBranch;

public record GetRoomsByBranchQuery(Guid BranchId) : IRequest<List<RoomDto>>;

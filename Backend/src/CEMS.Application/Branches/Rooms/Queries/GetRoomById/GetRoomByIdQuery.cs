using MediatR;

namespace CEMS.Application.Branches.Rooms.Queries.GetRoomById;

public record GetRoomByIdQuery(Guid Id) : IRequest<RoomDto>;

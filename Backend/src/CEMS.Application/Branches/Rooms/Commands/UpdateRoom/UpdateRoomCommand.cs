using MediatR;

namespace CEMS.Application.Branches.Rooms.Commands.UpdateRoom;

public record UpdateRoomCommand(Guid Id, string Name, int Capacity) : IRequest<RoomDto>;

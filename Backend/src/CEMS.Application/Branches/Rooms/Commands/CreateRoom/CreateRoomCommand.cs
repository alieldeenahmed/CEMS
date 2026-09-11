using CEMS.Application.Branches;
using MediatR;

namespace CEMS.Application.Branches.Rooms.Commands.CreateRoom;

public record CreateRoomCommand(Guid BranchId, string Name, int Capacity) : IRequest<RoomDto>;

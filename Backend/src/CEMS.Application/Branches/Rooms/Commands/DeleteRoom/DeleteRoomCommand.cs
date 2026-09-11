using MediatR;

namespace CEMS.Application.Branches.Rooms.Commands.DeleteRoom;

public record DeleteRoomCommand(Guid Id) : IRequest;

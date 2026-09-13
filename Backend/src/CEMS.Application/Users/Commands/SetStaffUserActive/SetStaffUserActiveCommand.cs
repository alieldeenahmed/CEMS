using MediatR;

namespace CEMS.Application.Users.Commands.SetStaffUserActive;

public record SetStaffUserActiveCommand(Guid UserId, bool IsActive) : IRequest;

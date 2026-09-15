using MediatR;

namespace CEMS.Application.Users.Commands.ResetStaffPassword;

public record ResetStaffPasswordCommand(Guid UserId, string NewPassword) : IRequest;

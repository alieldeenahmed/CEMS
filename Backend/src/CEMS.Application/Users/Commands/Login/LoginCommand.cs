using MediatR;

namespace CEMS.Application.Users.Commands.Login;

public record LoginCommand(string Email, string Password) : IRequest<AuthResultDto>;

using MediatR;

namespace CEMS.Application.Users.Commands.Register;

public record RegisterCommand(string Email, string Password, string FullName, string PhoneNumber) : IRequest<AuthResultDto>;

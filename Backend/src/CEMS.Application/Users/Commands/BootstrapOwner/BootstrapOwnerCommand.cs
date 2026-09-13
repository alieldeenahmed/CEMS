using MediatR;

namespace CEMS.Application.Users.Commands.BootstrapOwner;

public record BootstrapOwnerCommand(string Email, string Password, string FullName, string PhoneNumber) : IRequest<AuthResultDto>;

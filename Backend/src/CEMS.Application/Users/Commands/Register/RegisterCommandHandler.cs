using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Users;
using MediatR;

namespace CEMS.Application.Users.Commands.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResultDto>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public RegisterCommandHandler(IIdentityService identityService, IJwtTokenGenerator tokenGenerator)
    {
        _identityService = identityService;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<AuthResultDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.CreateUserAsync(
            request.Email, request.Password, request.FullName, request.PhoneNumber, RoleNames.Parent);

        if (!result.Succeeded)
        {
            throw new BadRequestException(result.Errors);
        }

        var authenticatedUser = await _identityService.GetAuthenticatedUserAsync(result.UserId);
        var (token, expiresAtUtc) = _tokenGenerator.GenerateToken(authenticatedUser);

        return new AuthResultDto(
            authenticatedUser.UserId, authenticatedUser.Email, authenticatedUser.FullName,
            authenticatedUser.Roles, token, expiresAtUtc);
    }
}

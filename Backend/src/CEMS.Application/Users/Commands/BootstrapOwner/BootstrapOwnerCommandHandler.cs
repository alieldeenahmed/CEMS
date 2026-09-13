using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Users;
using MediatR;

namespace CEMS.Application.Users.Commands.BootstrapOwner;

public class BootstrapOwnerCommandHandler : IRequestHandler<BootstrapOwnerCommand, AuthResultDto>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public BootstrapOwnerCommandHandler(IIdentityService identityService, IJwtTokenGenerator tokenGenerator)
    {
        _identityService = identityService;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<AuthResultDto> Handle(BootstrapOwnerCommand request, CancellationToken cancellationToken)
    {
        if (await _identityService.AnyUsersExistAsync())
        {
            throw new ForbiddenAccessException("An account already exists; the Owner account can only be bootstrapped once.");
        }

        var result = await _identityService.CreateUserAsync(request.Email, request.Password, request.FullName, request.PhoneNumber, RoleNames.Owner);
        if (!result.Succeeded)
        {
            throw new BadRequestException(result.Errors);
        }

        var user = await _identityService.GetAuthenticatedUserAsync(result.UserId);
        var (token, expiresAtUtc) = _tokenGenerator.GenerateToken(user);

        return new AuthResultDto(user.UserId, user.Email, user.FullName, user.Roles, token, expiresAtUtc);
    }
}

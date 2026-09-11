using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using MediatR;

namespace CEMS.Application.Users.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResultDto>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public LoginCommandHandler(IIdentityService identityService, IJwtTokenGenerator tokenGenerator)
    {
        _identityService = identityService;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<AuthResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _identityService.ValidateCredentialsAsync(request.Email, request.Password)
            ?? throw new AuthenticationException("Invalid email or password.");

        var (token, expiresAtUtc) = _tokenGenerator.GenerateToken(user);

        return new AuthResultDto(user.UserId, user.Email, user.FullName, user.Roles, token, expiresAtUtc);
    }
}

using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Students;
using CEMS.Domain.Users;
using MediatR;

namespace CEMS.Application.Users.Commands.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResultDto>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IApplicationDbContext _context;

    public RegisterCommandHandler(IIdentityService identityService, IJwtTokenGenerator tokenGenerator, IApplicationDbContext context)
    {
        _identityService = identityService;
        _tokenGenerator = tokenGenerator;
        _context = context;
    }

    public async Task<AuthResultDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.CreateUserAsync(
            request.Email, request.Password, request.FullName, request.PhoneNumber, RoleNames.Parent);

        if (!result.Succeeded)
        {
            throw new BadRequestException(result.Errors);
        }

        _context.Guardians.Add(new Guardian
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Phone = request.PhoneNumber,
            Email = request.Email,
            UserId = result.UserId
        });
        await _context.SaveChangesAsync(cancellationToken);

        var authenticatedUser = await _identityService.GetAuthenticatedUserAsync(result.UserId);
        var (token, expiresAtUtc) = _tokenGenerator.GenerateToken(authenticatedUser);

        return new AuthResultDto(
            authenticatedUser.UserId, authenticatedUser.Email, authenticatedUser.FullName,
            authenticatedUser.Roles, token, expiresAtUtc);
    }
}

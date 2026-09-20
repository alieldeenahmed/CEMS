using CEMS.Application.Common.Concurrency;
using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Users;
using MediatR;

namespace CEMS.Application.Users.Commands.BootstrapOwner;

public class BootstrapOwnerCommandHandler : IRequestHandler<BootstrapOwnerCommand, AuthResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public BootstrapOwnerCommandHandler(IApplicationDbContext context, IIdentityService identityService, IJwtTokenGenerator tokenGenerator)
    {
        _context = context;
        _identityService = identityService;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<AuthResultDto> Handle(BootstrapOwnerCommand request, CancellationToken cancellationToken)
    {
        // "Nobody exists yet, so create the first Owner" is a check-then-write on the whole user table:
        // two simultaneous first requests would both pass the check and create two Owners. Serialize them.
        await using var transaction = await _context.BeginLockedTransactionAsync(cancellationToken, LockKeys.OwnerBootstrap);

        if (await _identityService.AnyUsersExistAsync())
        {
            throw new ForbiddenAccessException("An account already exists; the Owner account can only be bootstrapped once.");
        }

        var result = await _identityService.CreateUserAsync(request.Email, request.Password, request.FullName, request.PhoneNumber, RoleNames.Owner);
        if (!result.Succeeded)
        {
            throw new BadRequestException(result.Errors);
        }

        await transaction.CommitAsync(cancellationToken);

        var user = await _identityService.GetAuthenticatedUserAsync(result.UserId);
        var (token, expiresAtUtc) = _tokenGenerator.GenerateToken(user);

        return new AuthResultDto(user.UserId, user.Email, user.FullName, user.Roles, token, expiresAtUtc);
    }
}

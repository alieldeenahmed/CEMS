using CEMS.Application.Common.Interfaces;
using MediatR;

namespace CEMS.Application.Users.Commands.SetStaffUserActive;

public class SetStaffUserActiveCommandHandler : IRequestHandler<SetStaffUserActiveCommand>
{
    private readonly IIdentityService _identityService;

    public SetStaffUserActiveCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task Handle(SetStaffUserActiveCommand request, CancellationToken cancellationToken)
    {
        await _identityService.SetUserActiveAsync(request.UserId, request.IsActive);
    }
}

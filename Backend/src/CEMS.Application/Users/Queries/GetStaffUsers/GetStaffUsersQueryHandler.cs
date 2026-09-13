using CEMS.Application.Common.Interfaces;
using MediatR;

namespace CEMS.Application.Users.Queries.GetStaffUsers;

public class GetStaffUsersQueryHandler : IRequestHandler<GetStaffUsersQuery, List<StaffUserDto>>
{
    private readonly IIdentityService _identityService;

    public GetStaffUsersQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<List<StaffUserDto>> Handle(GetStaffUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _identityService.GetStaffUsersAsync();

        return users
            .Select(u => new StaffUserDto(u.UserId, u.Email, u.FullName, u.Roles, u.BranchIds, u.IsActive))
            .ToList();
    }
}

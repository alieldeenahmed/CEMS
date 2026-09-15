using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Users.Queries.GetBranchStaff;

public class GetBranchStaffQueryHandler : IRequestHandler<GetBranchStaffQuery, List<StaffUserDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUser;

    public GetBranchStaffQueryHandler(IApplicationDbContext context, IIdentityService identityService, ICurrentUserService currentUser)
    {
        _context = context;
        _identityService = identityService;
        _currentUser = currentUser;
    }

    public async Task<List<StaffUserDto>> Handle(GetBranchStaffQuery request, CancellationToken cancellationToken)
    {
        var myBranchIds = _currentUser.BranchIds;

        var frontDeskUsers = await _identityService.GetUsersInRoleAsync(RoleNames.FrontDesk);
        var frontDeskDtos = frontDeskUsers
            .Where(u => u.BranchIds.Any(myBranchIds.Contains))
            .Select(u => new StaffUserDto(u.UserId, u.Email, u.FullName, u.Roles, u.BranchIds, u.IsActive));

        // A Teacher's branch lives on their Teacher profile (TeacherBranch), not on the staff account
        // (UserBranchAssignments is only populated for FrontDesk/BranchManager at creation), so it's
        // looked up separately -- same asymmetry ResetStaffPassword already has to account for. Only
        // the branches that overlap this Branch Manager's own branch(es) are kept, so a floating
        // teacher's assignment to a branch this manager doesn't run is never exposed here.
        var teacherBranchesAtMyBranches = await _context.TeacherBranches
            .Where(tb => myBranchIds.Contains(tb.BranchId))
            .Select(tb => new { tb.Teacher.UserId, tb.BranchId })
            .ToListAsync(cancellationToken);

        var teacherBranchesByUserId = teacherBranchesAtMyBranches
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(x => x.BranchId).ToList());

        var teacherUsers = await _identityService.GetUsersInRoleAsync(RoleNames.Teacher);
        var teacherDtos = teacherUsers
            .Where(u => teacherBranchesByUserId.ContainsKey(u.UserId))
            .Select(u => new StaffUserDto(u.UserId, u.Email, u.FullName, u.Roles, teacherBranchesByUserId[u.UserId], u.IsActive));

        return frontDeskDtos
            .Concat(teacherDtos)
            .OrderBy(u => u.FullName)
            .ToList();
    }
}

using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Teachers.Queries.GetTeacherCandidates;

// A minimal, purpose-built alternative to the Owner-only staff list: Branch Manager can create a
// teacher profile (see CreateTeacherCommand), but broadening GetStaffUsers to let them pick from
// would leak every staff member's email, roles, and active status org-wide, breaking the
// branch-isolation principle enforced everywhere else. This returns only what the picker needs.
public class GetTeacherCandidatesQueryHandler : IRequestHandler<GetTeacherCandidatesQuery, List<TeacherCandidateDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public GetTeacherCandidatesQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<List<TeacherCandidateDto>> Handle(GetTeacherCandidatesQuery request, CancellationToken cancellationToken)
    {
        var teacherRoleUsers = await _identityService.GetUsersInRoleAsync(RoleNames.Teacher);
        var alreadyProfiledUserIds = await _context.Teachers.Select(t => t.UserId).ToListAsync(cancellationToken);

        return teacherRoleUsers
            .Where(u => !alreadyProfiledUserIds.Contains(u.UserId))
            .Select(u => new TeacherCandidateDto(u.UserId, u.FullName, u.Email))
            .ToList();
    }
}

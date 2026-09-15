using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Users.Commands.ResetStaffPassword;

public class ResetStaffPasswordCommandHandler : IRequestHandler<ResetStaffPasswordCommand>
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUser;
    private readonly IApplicationDbContext _context;

    public ResetStaffPasswordCommandHandler(IIdentityService identityService, ICurrentUserService currentUser, IApplicationDbContext context)
    {
        _identityService = identityService;
        _currentUser = currentUser;
        _context = context;
    }

    public async Task Handle(ResetStaffPasswordCommand request, CancellationToken cancellationToken)
    {
        var targetUser = await _identityService.GetAuthenticatedUserAsync(request.UserId);

        if (!_currentUser.IsInRole(RoleNames.Owner))
        {
            // A Branch Manager can only reset a Teacher or Front Desk account at their own branch --
            // never another Owner's or Branch Manager's password, same boundary CreateStaffUser
            // enforces. A Teacher's branch lives on their Teacher profile (TeacherBranch), not on the
            // staff account -- UserBranchAssignments is only populated for FrontDesk/BranchManager at
            // creation -- so which table to check depends on the target's role.
            var isEligibleRole = targetUser.Roles.All(role => role == RoleNames.Teacher || role == RoleNames.FrontDesk);

            var targetBranchIds = targetUser.Roles.Contains(RoleNames.Teacher)
                ? await _context.TeacherBranches
                    .Where(tb => tb.Teacher.UserId == request.UserId)
                    .Select(tb => tb.BranchId)
                    .ToListAsync(cancellationToken)
                : targetUser.BranchIds;

            var isOwnBranchStaff = _currentUser.IsInRole(RoleNames.BranchManager)
                && isEligibleRole
                && targetBranchIds.Any(branchId => _currentUser.HasAccessToBranch(branchId));

            if (!isOwnBranchStaff)
            {
                throw new ForbiddenAccessException("You do not have access to reset this account's password.");
            }
        }

        var result = await _identityService.ResetPasswordAsync(request.UserId, request.NewPassword);
        if (!result.Succeeded)
        {
            throw new BadRequestException(result.Errors);
        }
    }
}

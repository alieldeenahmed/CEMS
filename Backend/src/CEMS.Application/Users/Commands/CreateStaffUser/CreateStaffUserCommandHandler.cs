using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Branches;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Users.Commands.CreateStaffUser;

public class CreateStaffUserCommandHandler : IRequestHandler<CreateStaffUserCommand, StaffUserDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUser;

    public CreateStaffUserCommandHandler(IApplicationDbContext context, IIdentityService identityService, ICurrentUserService currentUser)
    {
        _context = context;
        _identityService = identityService;
        _currentUser = currentUser;
    }

    public async Task<StaffUserDto> Handle(CreateStaffUserCommand request, CancellationToken cancellationToken)
    {
        var isOwner = _currentUser.IsInRole(RoleNames.Owner);

        if (request.Role == RoleNames.BranchManager && !isOwner)
        {
            throw new ForbiddenAccessException("Only Owner can create BranchManager accounts.");
        }

        if (request.BranchId.HasValue)
        {
            var branchExists = await _context.Branches.AnyAsync(b => b.Id == request.BranchId.Value, cancellationToken);
            if (!branchExists)
            {
                throw new NotFoundException(nameof(Branch), request.BranchId.Value);
            }

            if (!isOwner && !_currentUser.HasAccessToBranch(request.BranchId.Value))
            {
                throw new ForbiddenAccessException("You can only create staff accounts for your own branch.");
            }
        }

        var result = await _identityService.CreateUserAsync(
            request.Email, request.Password, request.FullName, request.PhoneNumber, request.Role);

        if (!result.Succeeded)
        {
            throw new BadRequestException(result.Errors);
        }

        if (request.BranchId.HasValue)
        {
            _context.UserBranchAssignments.Add(new UserBranchAssignment
            {
                Id = Guid.NewGuid(),
                UserId = result.UserId,
                BranchId = request.BranchId.Value
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        var authenticatedUser = await _identityService.GetAuthenticatedUserAsync(result.UserId);

        return new StaffUserDto(
            authenticatedUser.UserId, authenticatedUser.Email, authenticatedUser.FullName,
            authenticatedUser.Roles, authenticatedUser.BranchIds, authenticatedUser.IsActive);
    }
}

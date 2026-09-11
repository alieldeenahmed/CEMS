using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Application.Students;
using CEMS.Domain.Students;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Students.Guardians.Queries.GetGuardianById;

public class GetGuardianByIdQueryHandler : IRequestHandler<GetGuardianByIdQuery, GuardianDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetGuardianByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<GuardianDto> Handle(GetGuardianByIdQuery request, CancellationToken cancellationToken)
    {
        var guardian = await _context.Guardians.FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Guardian), request.Id);

        var isOwnRecord = _currentUser.IsInRole(RoleNames.Parent) && guardian.UserId == _currentUser.UserId;
        var isStaff = _currentUser.IsInRole(RoleNames.Owner) || _currentUser.IsInRole(RoleNames.BranchManager) || _currentUser.IsInRole(RoleNames.FrontDesk);

        if (!isStaff && !isOwnRecord)
        {
            throw new ForbiddenAccessException("You do not have access to this guardian record.");
        }

        return GuardianDto.FromEntity(guardian);
    }
}

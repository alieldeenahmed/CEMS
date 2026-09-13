using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Teachers.Queries.GetTeacherById;

public class GetTeacherByIdQueryHandler : IRequestHandler<GetTeacherByIdQuery, TeacherDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IIdentityService _identityService;

    public GetTeacherByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser, IIdentityService identityService)
    {
        _context = context;
        _currentUser = currentUser;
        _identityService = identityService;
    }

    public async Task<TeacherDto> Handle(GetTeacherByIdQuery request, CancellationToken cancellationToken)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Teacher), request.Id);

        var branchIds = await _context.TeacherBranches
            .Where(tb => tb.TeacherId == teacher.Id)
            .Select(tb => tb.BranchId)
            .ToListAsync(cancellationToken);

        var isOwnRecord = teacher.UserId == _currentUser.UserId;
        var hasAccess = _currentUser.IsInRole(RoleNames.Owner)
            || isOwnRecord
            || branchIds.Any(_currentUser.HasAccessToBranch);

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this teacher.");
        }

        var user = await _identityService.GetAuthenticatedUserAsync(teacher.UserId);

        return new TeacherDto(teacher.Id, teacher.UserId, user.FullName, user.Email, teacher.HireDate, teacher.PayType, teacher.PayRate, branchIds);
    }
}

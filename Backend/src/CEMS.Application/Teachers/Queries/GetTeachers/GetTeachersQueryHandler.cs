using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Teachers.Queries.GetTeachers;

public class GetTeachersQueryHandler : IRequestHandler<GetTeachersQuery, List<TeacherDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IIdentityService _identityService;

    public GetTeachersQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser, IIdentityService identityService)
    {
        _context = context;
        _currentUser = currentUser;
        _identityService = identityService;
    }

    public async Task<List<TeacherDto>> Handle(GetTeachersQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Teacher> query = _context.Teachers;

        if (!_currentUser.IsInRole(RoleNames.Owner))
        {
            var branchIds = _currentUser.BranchIds;
            query = query.Where(t => t.TeacherBranches.Any(tb => branchIds.Contains(tb.BranchId)));
        }

        var teachers = await query
            .OrderBy(t => t.HireDate)
            .Select(t => new
            {
                t.Id,
                t.UserId,
                t.HireDate,
                t.PayType,
                t.PayRate,
                BranchIds = t.TeacherBranches.Select(tb => tb.BranchId).ToList()
            })
            .ToListAsync(cancellationToken);

        var result = new List<TeacherDto>();
        foreach (var teacher in teachers)
        {
            var user = await _identityService.GetAuthenticatedUserAsync(teacher.UserId);
            var showPay = TeacherDto.CanSeePay(_currentUser, teacher.UserId);
            result.Add(new TeacherDto(teacher.Id, teacher.UserId, user.FullName, user.Email, teacher.HireDate,
                showPay ? teacher.PayType : null, showPay ? teacher.PayRate : null, teacher.BranchIds));
        }

        return result;
    }
}

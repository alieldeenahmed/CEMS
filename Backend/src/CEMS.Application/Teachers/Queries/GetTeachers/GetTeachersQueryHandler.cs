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

    public GetTeachersQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<TeacherDto>> Handle(GetTeachersQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Teacher> query = _context.Teachers;

        if (!_currentUser.IsInRole(RoleNames.Owner))
        {
            var branchIds = _currentUser.BranchIds;
            query = query.Where(t => t.TeacherBranches.Any(tb => branchIds.Contains(tb.BranchId)));
        }

        return await query
            .OrderBy(t => t.HireDate)
            .Select(t => new TeacherDto(t.Id, t.UserId, t.HireDate, t.PayType, t.PayRate, t.TeacherBranches.Select(tb => tb.BranchId).ToList()))
            .ToListAsync(cancellationToken);
    }
}

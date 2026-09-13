using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Students;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Students.Queries.GetStudents;

public class GetStudentsQueryHandler : IRequestHandler<GetStudentsQuery, List<StudentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetStudentsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<StudentDto>> Handle(GetStudentsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Student> query;

        if (_currentUser.IsInRole(RoleNames.Owner))
        {
            query = _context.Students;
        }
        else
        {
            var branchIds = _currentUser.BranchIds;
            query = _context.Students.Where(s => branchIds.Contains(s.CurrentBranchId));
        }

        return await query
            .OrderBy(s => s.FullName)
            .Select(s => new StudentDto(s.Id, s.FullName, s.DateOfBirth, s.Gender, s.EnrollmentDate, s.Status, s.CurrentBranchId))
            .ToListAsync(cancellationToken);
    }
}

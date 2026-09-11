using CEMS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Students.Queries.GetMyChildren;

public class GetMyChildrenQueryHandler : IRequestHandler<GetMyChildrenQuery, List<StudentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyChildrenQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<StudentDto>> Handle(GetMyChildrenQuery request, CancellationToken cancellationToken)
    {
        return await _context.StudentGuardians
            .Where(sg => sg.Guardian.UserId == _currentUser.UserId)
            .Select(sg => sg.Student)
            .OrderBy(s => s.FullName)
            .Select(s => new StudentDto(s.Id, s.FullName, s.DateOfBirth, s.Gender, s.EnrollmentDate, s.Status, s.CurrentBranchId))
            .ToListAsync(cancellationToken);
    }
}

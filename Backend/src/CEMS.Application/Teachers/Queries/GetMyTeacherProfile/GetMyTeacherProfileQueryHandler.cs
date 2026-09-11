using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Teachers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Teachers.Queries.GetMyTeacherProfile;

public class GetMyTeacherProfileQueryHandler : IRequestHandler<GetMyTeacherProfileQuery, TeacherDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyTeacherProfileQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<TeacherDto> Handle(GetMyTeacherProfileQuery request, CancellationToken cancellationToken)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("TeacherProfile", _currentUser.UserId ?? Guid.Empty);

        var branchIds = await _context.TeacherBranches
            .Where(tb => tb.TeacherId == teacher.Id)
            .Select(tb => tb.BranchId)
            .ToListAsync(cancellationToken);

        return new TeacherDto(teacher.Id, teacher.UserId, teacher.HireDate, teacher.PayType, teacher.PayRate, branchIds);
    }
}

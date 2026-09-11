using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Application.Students;
using CEMS.Domain.Students;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Students.Queries.GetGuardiansForStudent;

public class GetGuardiansForStudentQueryHandler : IRequestHandler<GetGuardiansForStudentQuery, List<GuardianDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetGuardiansForStudentQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<GuardianDto>> Handle(GetGuardiansForStudentQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        var hasAccess = _currentUser.IsInRole(RoleNames.Owner)
            || (_currentUser.IsInRole(RoleNames.Parent) && await _context.StudentGuardians
                .AnyAsync(sg => sg.StudentId == student.Id && sg.Guardian.UserId == _currentUser.UserId, cancellationToken))
            || _currentUser.HasAccessToBranch(student.CurrentBranchId);

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this student.");
        }

        return await _context.StudentGuardians
            .Where(sg => sg.StudentId == request.StudentId)
            .Select(sg => new GuardianDto(sg.Guardian.Id, sg.Guardian.FullName, sg.Guardian.Phone, sg.Guardian.Email, sg.Guardian.UserId))
            .ToListAsync(cancellationToken);
    }
}

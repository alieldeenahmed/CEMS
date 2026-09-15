using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Students.Queries.GetBranchHistoryForStudent;

public class GetBranchHistoryForStudentQueryHandler : IRequestHandler<GetBranchHistoryForStudentQuery, List<StudentBranchHistoryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetBranchHistoryForStudentQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<StudentBranchHistoryDto>> Handle(GetBranchHistoryForStudentQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        if (!_currentUser.HasAccessToBranch(student.CurrentBranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this student.");
        }

        return await _context.StudentBranchHistories
            .Where(h => h.StudentId == request.StudentId)
            .OrderByDescending(h => h.TransferDate)
            .Select(h => new StudentBranchHistoryDto(
                h.Id,
                h.StudentId,
                h.FromBranchId,
                h.FromBranch.Name,
                h.ToBranchId,
                h.ToBranch.Name,
                h.TransferDate,
                h.Reason,
                h.TransferredByUserId))
            .ToListAsync(cancellationToken);
    }
}

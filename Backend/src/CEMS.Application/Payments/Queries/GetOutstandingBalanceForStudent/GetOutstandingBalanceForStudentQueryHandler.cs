using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payments;
using CEMS.Domain.Students;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payments.Queries.GetOutstandingBalanceForStudent;

public class GetOutstandingBalanceForStudentQueryHandler : IRequestHandler<GetOutstandingBalanceForStudentQuery, OutstandingBalanceDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetOutstandingBalanceForStudentQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<OutstandingBalanceDto> Handle(GetOutstandingBalanceForStudentQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        var hasAccess = _currentUser.IsInRole(RoleNames.Owner) || _currentUser.HasAccessToBranch(student.CurrentBranchId);

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this student.");
        }

        var total = await _context.Invoices
            .Where(i => i.StudentId == request.StudentId && i.Status != InvoiceStatus.Cancelled)
            .Select(i => i.Amount - i.Payments.Sum(p => p.AmountPaid))
            .SumAsync(cancellationToken);

        return new OutstandingBalanceDto(request.StudentId, total);
    }
}

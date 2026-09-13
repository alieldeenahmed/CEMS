using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payments;
using CEMS.Domain.Students;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payments.Queries.GetInvoicesForStudent;

public class GetInvoicesForStudentQueryHandler : IRequestHandler<GetInvoicesForStudentQuery, List<InvoiceDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetInvoicesForStudentQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<InvoiceDto>> Handle(GetInvoicesForStudentQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        var hasAccess = _currentUser.IsInRole(RoleNames.Owner) || _currentUser.HasAccessToBranch(student.CurrentBranchId);

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this student.");
        }

        var rows = await _context.Invoices
            .Where(i => i.StudentId == request.StudentId)
            .OrderByDescending(i => i.IssuedDate)
            .Select(i => new
            {
                i.Id,
                i.StudentId,
                i.PackageId,
                i.Amount,
                i.IssuedDate,
                i.DueDate,
                i.Status,
                AmountPaid = i.Payments.Sum(p => p.AmountPaid)
            })
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return rows.Select(row =>
        {
            var isOverdue = row.Status is InvoiceStatus.Pending or InvoiceStatus.PartiallyPaid && row.DueDate < today;
            return new InvoiceDto(row.Id, row.StudentId, row.PackageId, row.Amount, row.AmountPaid, row.Amount - row.AmountPaid, row.IssuedDate, row.DueDate, row.Status, isOverdue);
        }).ToList();
    }
}

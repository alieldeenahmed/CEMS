using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payments;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payments.Queries.GetPaymentsForInvoice;

public class GetPaymentsForInvoiceQueryHandler : IRequestHandler<GetPaymentsForInvoiceQuery, List<PaymentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetPaymentsForInvoiceQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<PaymentDto>> Handle(GetPaymentsForInvoiceQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Student)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), request.InvoiceId);

        var hasAccess = _currentUser.IsInRole(RoleNames.Owner) || _currentUser.HasAccessToBranch(invoice.Student.CurrentBranchId);

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this invoice.");
        }

        return await _context.Payments
            .Where(p => p.InvoiceId == request.InvoiceId)
            .OrderBy(p => p.PaymentDate)
            .Select(p => new PaymentDto(p.Id, p.InvoiceId, p.AmountPaid, p.PaymentDate, p.Method, p.ReceivedByUserId))
            .ToListAsync(cancellationToken);
    }
}

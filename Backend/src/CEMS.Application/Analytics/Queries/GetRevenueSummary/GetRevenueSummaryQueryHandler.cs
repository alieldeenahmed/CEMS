using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payments;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Analytics.Queries.GetRevenueSummary;

public class GetRevenueSummaryQueryHandler : IRequestHandler<GetRevenueSummaryQuery, RevenueSummaryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetRevenueSummaryQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<RevenueSummaryDto> Handle(GetRevenueSummaryQuery request, CancellationToken cancellationToken)
    {
        AnalyticsAccess.EnsureAccess(_currentUser, request.BranchId);

        var invoices = _context.Invoices
            .Where(i => i.Status != InvoiceStatus.Cancelled && i.IssuedDate >= request.PeriodStart && i.IssuedDate <= request.PeriodEnd);

        if (request.BranchId.HasValue)
        {
            invoices = invoices.Where(i => i.Student.CurrentBranchId == request.BranchId.Value);
        }

        var totalInvoiced = await invoices.SumAsync(i => i.Amount, cancellationToken);
        var totalOutstanding = await invoices.SumAsync(i => i.Amount - i.Payments.Sum(p => p.AmountPaid), cancellationToken);

        var payments = _context.Payments
            .Where(p => p.PaymentDate >= request.PeriodStart && p.PaymentDate <= request.PeriodEnd && p.Invoice.Status != InvoiceStatus.Cancelled);

        if (request.BranchId.HasValue)
        {
            payments = payments.Where(p => p.Invoice.Student.CurrentBranchId == request.BranchId.Value);
        }

        var totalCollected = await payments.SumAsync(p => p.AmountPaid, cancellationToken);

        return new RevenueSummaryDto(totalInvoiced, totalCollected, totalOutstanding);
    }
}

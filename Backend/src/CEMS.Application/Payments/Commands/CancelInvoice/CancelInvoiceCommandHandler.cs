using CEMS.Application.Common.Concurrency;
using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payments.Commands.CancelInvoice;

public class CancelInvoiceCommandHandler : IRequestHandler<CancelInvoiceCommand, InvoiceDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CancelInvoiceCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<InvoiceDto> Handle(CancelInvoiceCommand request, CancellationToken cancellationToken)
    {
        // Cancelling races with recording a payment (a payment landing after the "not paid" check would
        // leave a cancelled invoice with money against it), so it takes the same per-invoice lock.
        await using var transaction = await _context.BeginLockedTransactionAsync(cancellationToken, LockKeys.Invoice(request.Id));

        var invoice = await _context.Invoices
            .Include(i => i.Student)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), request.Id);

        _currentUser.EnsureAccessToBranch(invoice.Student.CurrentBranchId);

        if (invoice.Status == InvoiceStatus.Paid)
        {
            throw new BadRequestException(new[] { "Cannot cancel a fully paid invoice." });
        }

        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            throw new BadRequestException(new[] { "This invoice is already cancelled." });
        }

        invoice.Status = InvoiceStatus.Cancelled;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return InvoiceDto.FromEntity(invoice, DateOnly.FromDateTime(DateTime.UtcNow));
    }
}

using CEMS.Application.Common.Concurrency;
using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payments.Commands.RecordPayment;

public class RecordPaymentCommandHandler : IRequestHandler<RecordPaymentCommand, PaymentDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RecordPaymentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaymentDto> Handle(RecordPaymentCommand request, CancellationToken cancellationToken)
    {
        // A payment is read-modify-write on the invoice (sum what was paid, then derive its status), so two
        // simultaneous payments would each see the other missing and could overpay it or leave the wrong
        // status. Serialize them per invoice, and read the invoice only once the lock is held.
        await using var transaction = await _context.BeginLockedTransactionAsync(cancellationToken, LockKeys.Invoice(request.InvoiceId));

        var invoice = await _context.Invoices
            .Include(i => i.Student)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), request.InvoiceId);

        _currentUser.EnsureAccessToBranch(invoice.Student.CurrentBranchId);

        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            throw new BadRequestException(new[] { "Cannot record a payment against a cancelled invoice." });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.PaymentDate > today.AddDays(1))   // a day of slack for branches ahead of UTC
        {
            throw new BadRequestException(new[] { "The payment date cannot be in the future." });
        }

        // Summed before the new payment is added to the context: EF's relationship fix-up appends it to
        // invoice.Payments the moment it's tracked, so summing afterwards would count it twice.
        var previouslyPaid = invoice.Payments.Sum(p => p.AmountPaid);

        var balance = invoice.Amount - previouslyPaid;
        if (request.AmountPaid > balance)
        {
            throw new BadRequestException(new[]
            {
                balance <= 0
                    ? "This invoice is already fully paid."
                    : $"The payment exceeds the remaining balance of {balance:0.00}."
            });
        }

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            AmountPaid = request.AmountPaid,
            PaymentDate = request.PaymentDate,
            Method = request.Method,
            ReceivedByUserId = _currentUser.UserId ?? Guid.Empty
        };

        _context.Payments.Add(payment);

        var totalPaid = previouslyPaid + request.AmountPaid;
        invoice.Status = totalPaid >= invoice.Amount ? InvoiceStatus.Paid
            : totalPaid > 0 ? InvoiceStatus.PartiallyPaid
            : InvoiceStatus.Pending;

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PaymentDto(payment.Id, payment.InvoiceId, payment.AmountPaid, payment.PaymentDate, payment.Method, payment.ReceivedByUserId);
    }
}

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
        var invoice = await _context.Invoices
            .Include(i => i.Student)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), request.InvoiceId);

        if (!_currentUser.HasAccessToBranch(invoice.Student.CurrentBranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            throw new BadRequestException(new[] { "Cannot record a payment against a cancelled invoice." });
        }

        // Summed before the new payment is added to the context: EF's relationship fix-up appends it to
        // invoice.Payments the moment it's tracked, so summing afterwards would count it twice.
        var previouslyPaid = invoice.Payments.Sum(p => p.AmountPaid);

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

        return new PaymentDto(payment.Id, payment.InvoiceId, payment.AmountPaid, payment.PaymentDate, payment.Method, payment.ReceivedByUserId);
    }
}

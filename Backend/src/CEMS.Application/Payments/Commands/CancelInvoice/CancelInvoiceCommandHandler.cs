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
        var invoice = await _context.Invoices
            .Include(i => i.Student)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), request.Id);

        if (!_currentUser.HasAccessToBranch(invoice.Student.CurrentBranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

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

        return InvoiceDto.FromEntity(invoice, DateOnly.FromDateTime(DateTime.UtcNow));
    }
}

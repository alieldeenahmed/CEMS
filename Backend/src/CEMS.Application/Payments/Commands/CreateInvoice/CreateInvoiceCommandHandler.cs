using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Payments;
using CEMS.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payments.Commands.CreateInvoice;

public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, InvoiceDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateInvoiceCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<InvoiceDto> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        _currentUser.EnsureAccessToBranch(student.CurrentBranchId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        decimal amount;

        if (request.PackageId.HasValue)
        {
            var package = await _context.Packages.Include(p => p.Course).FirstOrDefaultAsync(p => p.Id == request.PackageId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(Package), request.PackageId.Value);

            // A package belongs to a course at one branch and carries that branch's price; billing a student
            // from another branch against it would let one branch invoice with another's price list.
            if (package.Course.BranchId != student.CurrentBranchId)
            {
                throw new BadRequestException(new[] { "This package belongs to a course at a different branch than the student." });
            }

            amount = package.Price;
        }
        else
        {
            if (!request.Amount.HasValue)
            {
                throw new BadRequestException(new[] { "Amount is required for an ad-hoc invoice (no package)." });
            }

            amount = request.Amount.Value;
        }

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            PackageId = request.PackageId,
            Amount = amount,
            IssuedDate = today,
            DueDate = request.DueDate,
            Status = InvoiceStatus.Pending
        };

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        return InvoiceDto.FromEntity(invoice, today);
    }
}

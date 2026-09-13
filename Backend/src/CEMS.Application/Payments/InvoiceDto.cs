using CEMS.Domain.Payments;

namespace CEMS.Application.Payments;

public record InvoiceDto(
    Guid Id,
    Guid StudentId,
    Guid? PackageId,
    decimal Amount,
    decimal AmountPaid,
    decimal BalanceRemaining,
    DateOnly IssuedDate,
    DateOnly DueDate,
    InvoiceStatus Status,
    bool IsOverdue)
{
    public static InvoiceDto FromEntity(Invoice invoice, DateOnly today)
    {
        var amountPaid = invoice.Payments.Sum(p => p.AmountPaid);
        var isOverdue = invoice.Status is InvoiceStatus.Pending or InvoiceStatus.PartiallyPaid && invoice.DueDate < today;

        return new InvoiceDto(
            invoice.Id, invoice.StudentId, invoice.PackageId, invoice.Amount, amountPaid, invoice.Amount - amountPaid,
            invoice.IssuedDate, invoice.DueDate, invoice.Status, isOverdue);
    }
}

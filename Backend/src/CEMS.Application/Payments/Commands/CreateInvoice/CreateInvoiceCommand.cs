using MediatR;

namespace CEMS.Application.Payments.Commands.CreateInvoice;

public record CreateInvoiceCommand(Guid StudentId, Guid? PackageId, decimal? Amount, DateOnly DueDate) : IRequest<InvoiceDto>;

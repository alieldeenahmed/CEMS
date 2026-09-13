using MediatR;

namespace CEMS.Application.Payments.Commands.CancelInvoice;

public record CancelInvoiceCommand(Guid Id) : IRequest<InvoiceDto>;

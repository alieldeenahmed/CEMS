using MediatR;

namespace CEMS.Application.Payments.Queries.GetInvoiceById;

public record GetInvoiceByIdQuery(Guid Id) : IRequest<InvoiceDto>;

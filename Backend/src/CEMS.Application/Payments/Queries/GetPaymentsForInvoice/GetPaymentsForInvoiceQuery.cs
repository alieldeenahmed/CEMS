using MediatR;

namespace CEMS.Application.Payments.Queries.GetPaymentsForInvoice;

public record GetPaymentsForInvoiceQuery(Guid InvoiceId) : IRequest<List<PaymentDto>>;

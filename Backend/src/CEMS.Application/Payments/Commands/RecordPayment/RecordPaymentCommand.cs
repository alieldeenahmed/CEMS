using CEMS.Domain.Payments;
using MediatR;

namespace CEMS.Application.Payments.Commands.RecordPayment;

public record RecordPaymentCommand(Guid InvoiceId, decimal AmountPaid, DateOnly PaymentDate, PaymentMethod Method) : IRequest<PaymentDto>;

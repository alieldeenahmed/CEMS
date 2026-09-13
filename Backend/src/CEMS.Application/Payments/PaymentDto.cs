using CEMS.Domain.Payments;

namespace CEMS.Application.Payments;

public record PaymentDto(Guid Id, Guid InvoiceId, decimal AmountPaid, DateOnly PaymentDate, PaymentMethod Method, Guid ReceivedByUserId);

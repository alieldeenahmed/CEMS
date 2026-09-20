using CEMS.Application.Common.Validation;
using FluentValidation;

namespace CEMS.Application.Payments.Commands.RecordPayment;

public class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.AmountPaid).IsMoney();
        RuleFor(x => x.PaymentDate).NotEmpty();
        RuleFor(x => x.Method).IsInEnum();
    }
}

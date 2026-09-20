using CEMS.Application.Common.Validation;
using FluentValidation;

namespace CEMS.Application.Payments.Commands.CreateInvoice;

public class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.DueDate).NotEmpty();
        RuleFor(x => x.Amount).IsMoney().When(x => x.Amount.HasValue);
    }
}

using CEMS.Application.Common.Validation;
using FluentValidation;

namespace CEMS.Application.Payments.Commands.CreatePackage;

public class CreatePackageCommandValidator : AbstractValidator<CreatePackageCommand>
{
    public CreatePackageCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.SessionCount).GreaterThan(0);
        RuleFor(x => x.Price).IsMoney();
    }
}

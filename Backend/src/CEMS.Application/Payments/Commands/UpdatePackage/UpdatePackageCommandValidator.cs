using CEMS.Application.Common.Validation;
using FluentValidation;

namespace CEMS.Application.Payments.Commands.UpdatePackage;

public class UpdatePackageCommandValidator : AbstractValidator<UpdatePackageCommand>
{
    public UpdatePackageCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.SessionCount).GreaterThan(0);
        RuleFor(x => x.Price).IsMoney();
    }
}

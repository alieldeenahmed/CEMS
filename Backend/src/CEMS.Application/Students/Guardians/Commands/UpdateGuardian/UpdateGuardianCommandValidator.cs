using FluentValidation;

namespace CEMS.Application.Students.Guardians.Commands.UpdateGuardian;

public class UpdateGuardianCommandValidator : AbstractValidator<UpdateGuardianCommand>
{
    public UpdateGuardianCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

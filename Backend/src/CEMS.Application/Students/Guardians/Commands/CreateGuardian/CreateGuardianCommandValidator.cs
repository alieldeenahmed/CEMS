using FluentValidation;

namespace CEMS.Application.Students.Guardians.Commands.CreateGuardian;

public class CreateGuardianCommandValidator : AbstractValidator<CreateGuardianCommand>
{
    public CreateGuardianCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

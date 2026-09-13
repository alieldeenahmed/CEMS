using FluentValidation;

namespace CEMS.Application.Users.Commands.BootstrapOwner;

public class BootstrapOwnerCommandValidator : AbstractValidator<BootstrapOwnerCommand>
{
    public BootstrapOwnerCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PhoneNumber).NotEmpty();
    }
}

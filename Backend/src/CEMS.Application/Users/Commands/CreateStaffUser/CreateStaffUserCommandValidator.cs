using CEMS.Domain.Users;
using FluentValidation;

namespace CEMS.Application.Users.Commands.CreateStaffUser;

public class CreateStaffUserCommandValidator : AbstractValidator<CreateStaffUserCommand>
{
    private static readonly string[] AssignableRoles =
    {
        RoleNames.BranchManager,
        RoleNames.Teacher,
        RoleNames.FrontDesk
    };

    private static readonly string[] BranchRequiredRoles =
    {
        RoleNames.BranchManager,
        RoleNames.FrontDesk
    };

    public CreateStaffUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PhoneNumber).NotEmpty();

        RuleFor(x => x.Role)
            .Must(role => AssignableRoles.Contains(role))
            .WithMessage($"Role must be one of: {string.Join(", ", AssignableRoles)}.");

        RuleFor(x => x.BranchId)
            .NotEmpty()
            .When(x => BranchRequiredRoles.Contains(x.Role))
            .WithMessage("BranchId is required for BranchManager and FrontDesk accounts.");
    }
}

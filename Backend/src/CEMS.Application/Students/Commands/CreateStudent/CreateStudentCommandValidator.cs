using FluentValidation;

namespace CEMS.Application.Students.Commands.CreateStudent;

public class CreateStudentCommandValidator : AbstractValidator<CreateStudentCommand>
{
    public CreateStudentCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DateOfBirth).NotEmpty().LessThan(x => DateOnly.FromDateTime(DateTime.UtcNow));
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.RelationshipType).IsInEnum();

        RuleFor(x => x)
            .Must(x => x.ExistingGuardianId.HasValue || !string.IsNullOrWhiteSpace(x.NewGuardianFullName))
            .WithMessage("A guardian is required: link an existing one or provide details for a new one.");

        When(x => !x.ExistingGuardianId.HasValue, () =>
        {
            RuleFor(x => x.NewGuardianFullName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.NewGuardianPhone).NotEmpty().MaximumLength(30);
            RuleFor(x => x.NewGuardianEmail).NotEmpty().EmailAddress().MaximumLength(256);
        });
    }
}

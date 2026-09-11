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
    }
}

using FluentValidation;

namespace CEMS.Application.Students.Commands.UpdateStudent;

public class UpdateStudentCommandValidator : AbstractValidator<UpdateStudentCommand>
{
    public UpdateStudentCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DateOfBirth).NotEmpty().LessThan(x => DateOnly.FromDateTime(DateTime.UtcNow));
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
    }
}

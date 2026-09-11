using FluentValidation;

namespace CEMS.Application.Teachers.Commands.CreateTeacher;

public class CreateTeacherCommandValidator : AbstractValidator<CreateTeacherCommand>
{
    public CreateTeacherCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.HireDate).NotEmpty();
        RuleFor(x => x.PayType).IsInEnum();
        RuleFor(x => x.PayRate).GreaterThan(0);
    }
}

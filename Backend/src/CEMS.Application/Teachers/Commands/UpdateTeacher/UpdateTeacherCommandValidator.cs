using FluentValidation;

namespace CEMS.Application.Teachers.Commands.UpdateTeacher;

public class UpdateTeacherCommandValidator : AbstractValidator<UpdateTeacherCommand>
{
    public UpdateTeacherCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.HireDate).NotEmpty();
        RuleFor(x => x.PayType).IsInEnum();
        RuleFor(x => x.PayRate).GreaterThan(0);
    }
}

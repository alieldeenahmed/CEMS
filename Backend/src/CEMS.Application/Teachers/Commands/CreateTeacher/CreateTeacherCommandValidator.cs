using CEMS.Domain.Teachers;
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
        RuleFor(x => x.PayRate)
            .LessThanOrEqualTo(100)
            .When(x => x.PayType == PayType.Percentage)
            .WithMessage("A percentage pay rate cannot exceed 100.");
    }
}

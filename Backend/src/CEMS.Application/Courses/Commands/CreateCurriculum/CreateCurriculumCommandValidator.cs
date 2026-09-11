using FluentValidation;

namespace CEMS.Application.Courses.Commands.CreateCurriculum;

public class CreateCurriculumCommandValidator : AbstractValidator<CreateCurriculumCommand>
{
    public CreateCurriculumCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

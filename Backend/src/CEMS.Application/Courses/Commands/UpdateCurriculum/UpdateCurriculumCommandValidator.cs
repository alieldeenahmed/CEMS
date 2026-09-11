using FluentValidation;

namespace CEMS.Application.Courses.Commands.UpdateCurriculum;

public class UpdateCurriculumCommandValidator : AbstractValidator<UpdateCurriculumCommand>
{
    public UpdateCurriculumCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

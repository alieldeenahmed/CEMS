using FluentValidation;

namespace CEMS.Application.Courses.Commands.CreateSubject;

public class CreateSubjectCommandValidator : AbstractValidator<CreateSubjectCommand>
{
    public CreateSubjectCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CurriculumId).NotEmpty();
    }
}

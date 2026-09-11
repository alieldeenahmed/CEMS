using FluentValidation;

namespace CEMS.Application.Courses.Commands.UpdateSubject;

public class UpdateSubjectCommandValidator : AbstractValidator<UpdateSubjectCommand>
{
    public UpdateSubjectCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

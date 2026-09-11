using FluentValidation;

namespace CEMS.Application.Exams.Commands.CreateExam;

public class CreateExamCommandValidator : AbstractValidator<CreateExamCommand>
{
    public CreateExamCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaxScore).GreaterThan(0);
        RuleFor(x => x.ExamDate).NotEmpty();
    }
}

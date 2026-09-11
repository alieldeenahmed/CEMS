using FluentValidation;

namespace CEMS.Application.Exams.Commands.RecordGrade;

public class RecordGradeCommandValidator : AbstractValidator<RecordGradeCommand>
{
    public RecordGradeCommandValidator()
    {
        RuleFor(x => x.ExamId).NotEmpty();
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.Score).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Comments).MaximumLength(1000);
    }
}

using CEMS.Application.Common.Validation;
using FluentValidation;

namespace CEMS.Application.Exams.Commands.RecordGrade;

public class RecordGradeCommandValidator : AbstractValidator<RecordGradeCommand>
{
    public RecordGradeCommandValidator()
    {
        RuleFor(x => x.ExamId).NotEmpty();
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.Score).IsScore(allowZero: true);
        RuleFor(x => x.Comments).MaximumLength(1000);
    }
}

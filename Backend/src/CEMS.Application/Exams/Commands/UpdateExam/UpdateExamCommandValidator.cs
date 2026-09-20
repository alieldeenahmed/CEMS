using CEMS.Application.Common.Validation;
using FluentValidation;

namespace CEMS.Application.Exams.Commands.UpdateExam;

public class UpdateExamCommandValidator : AbstractValidator<UpdateExamCommand>
{
    public UpdateExamCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaxScore).IsScore(allowZero: false);
        RuleFor(x => x.ExamDate).NotEmpty();
    }
}

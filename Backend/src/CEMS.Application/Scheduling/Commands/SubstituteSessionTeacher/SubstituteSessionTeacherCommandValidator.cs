using FluentValidation;

namespace CEMS.Application.Scheduling.Commands.SubstituteSessionTeacher;

public class SubstituteSessionTeacherCommandValidator : AbstractValidator<SubstituteSessionTeacherCommand>
{
    public SubstituteSessionTeacherCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.NewTeacherId).NotEmpty();
    }
}

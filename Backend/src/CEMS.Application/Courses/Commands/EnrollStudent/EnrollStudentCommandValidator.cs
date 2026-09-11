using FluentValidation;

namespace CEMS.Application.Courses.Commands.EnrollStudent;

public class EnrollStudentCommandValidator : AbstractValidator<EnrollStudentCommand>
{
    public EnrollStudentCommandValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.CourseId).NotEmpty();
    }
}

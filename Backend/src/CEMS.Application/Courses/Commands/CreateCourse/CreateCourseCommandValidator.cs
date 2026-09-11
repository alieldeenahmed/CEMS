using FluentValidation;

namespace CEMS.Application.Courses.Commands.CreateCourse;

public class CreateCourseCommandValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DeliveryMode).IsInEnum();
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

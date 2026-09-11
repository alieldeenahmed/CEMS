using FluentValidation;

namespace CEMS.Application.Teachers.Commands.AddAvailability;

public class AddAvailabilityCommandValidator : AbstractValidator<AddAvailabilityCommand>
{
    public AddAvailabilityCommandValidator()
    {
        RuleFor(x => x.TeacherId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.DayOfWeek).IsInEnum();
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime);
    }
}

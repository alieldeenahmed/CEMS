using FluentValidation;

namespace CEMS.Application.Payroll.Commands.UpdateStaffPayrollRun;

public class UpdateStaffPayrollRunCommandValidator : AbstractValidator<UpdateStaffPayrollRunCommand>
{
    public UpdateStaffPayrollRunCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

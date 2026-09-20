using CEMS.Application.Common.Validation;
using FluentValidation;

namespace CEMS.Application.Payroll.Commands.UpdateStaffPayrollRun;

public class UpdateStaffPayrollRunCommandValidator : AbstractValidator<UpdateStaffPayrollRunCommand>
{
    public UpdateStaffPayrollRunCommandValidator()
    {
        RuleFor(x => x.Amount).IsMoney();
    }
}

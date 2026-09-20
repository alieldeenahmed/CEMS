using CEMS.Application.Common.Validation;
using FluentValidation;

namespace CEMS.Application.Payroll.Commands.GenerateStaffPayrollRun;

public class GenerateStaffPayrollRunCommandValidator : AbstractValidator<GenerateStaffPayrollRunCommand>
{
    public GenerateStaffPayrollRunCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PeriodEnd).GreaterThanOrEqualTo(x => x.PeriodStart);
        RuleFor(x => x.Amount).IsMoney();
    }
}

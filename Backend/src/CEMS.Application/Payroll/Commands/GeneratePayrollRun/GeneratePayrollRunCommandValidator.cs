using FluentValidation;

namespace CEMS.Application.Payroll.Commands.GeneratePayrollRun;

public class GeneratePayrollRunCommandValidator : AbstractValidator<GeneratePayrollRunCommand>
{
    public GeneratePayrollRunCommandValidator()
    {
        RuleFor(x => x.TeacherId).NotEmpty();
        RuleFor(x => x.PeriodEnd).GreaterThanOrEqualTo(x => x.PeriodStart);
    }
}

using FluentValidation;

namespace CEMS.Application.Students.Commands.TransferStudentBranch;

public class TransferStudentBranchCommandValidator : AbstractValidator<TransferStudentBranchCommand>
{
    public TransferStudentBranchCommandValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.NewBranchId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

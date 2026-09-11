using FluentValidation;

namespace CEMS.Application.Attendance.Commands.MarkAttendance;

public class MarkAttendanceCommandValidator : AbstractValidator<MarkAttendanceCommand>
{
    public MarkAttendanceCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}

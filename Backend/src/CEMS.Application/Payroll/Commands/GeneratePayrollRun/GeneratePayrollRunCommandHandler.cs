using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using CEMS.Domain.Payroll;
using CEMS.Domain.Teachers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payroll.Commands.GeneratePayrollRun;

public class GeneratePayrollRunCommandHandler : IRequestHandler<GeneratePayrollRunCommand, PayrollRunDto>
{
    private readonly IApplicationDbContext _context;

    public GeneratePayrollRunCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PayrollRunDto> Handle(GeneratePayrollRunCommand request, CancellationToken cancellationToken)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == request.TeacherId, cancellationToken)
            ?? throw new NotFoundException(nameof(Teacher), request.TeacherId);

        var overlapping = await _context.PayrollRuns.AnyAsync(
            r => r.TeacherId == request.TeacherId && r.PeriodStart <= request.PeriodEnd && request.PeriodStart <= r.PeriodEnd,
            cancellationToken);

        if (overlapping)
        {
            throw new BadRequestException(new[] { "A payroll run already exists for this teacher covering an overlapping period." });
        }

        // A session is paid if the teacher held it, regardless of student attendance. A session the
        // center cancelled is not paid, but its makeup session (linked separately) is a normal session
        // and is paid like any other -- per the earlier decision on cancellations vs no-shows.
        var periodStartUtc = request.PeriodStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var periodEndExclusiveUtc = request.PeriodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var sessions = await _context.CourseSessions
            .Where(s => s.TeacherId == request.TeacherId
                && s.Status != SessionStatus.Cancelled
                && s.StartUtc >= periodStartUtc
                && s.StartUtc < periodEndExclusiveUtc)
            .ToListAsync(cancellationToken);

        var payrollRun = new PayrollRun
        {
            Id = Guid.NewGuid(),
            TeacherId = request.TeacherId,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            Status = PayrollRunStatus.Draft
        };

        decimal totalAmount = 0;

        foreach (var session in sessions)
        {
            var amount = teacher.PayType == PayType.Hourly
                ? teacher.PayRate * (decimal)(session.EndUtc - session.StartUtc).TotalHours
                : teacher.PayRate;

            totalAmount += amount;

            payrollRun.LineItems.Add(new PayrollLineItem
            {
                Id = Guid.NewGuid(),
                PayrollRunId = payrollRun.Id,
                CourseSessionId = session.Id,
                Amount = amount
            });
        }

        payrollRun.TotalAmount = totalAmount;

        _context.PayrollRuns.Add(payrollRun);
        await _context.SaveChangesAsync(cancellationToken);

        return new PayrollRunDto(payrollRun.Id, payrollRun.TeacherId, payrollRun.PeriodStart, payrollRun.PeriodEnd, payrollRun.TotalAmount, payrollRun.Status);
    }
}

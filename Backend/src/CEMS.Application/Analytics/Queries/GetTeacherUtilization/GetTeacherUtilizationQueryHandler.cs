using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Analytics.Queries.GetTeacherUtilization;

public class GetTeacherUtilizationQueryHandler : IRequestHandler<GetTeacherUtilizationQuery, List<TeacherUtilizationDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IIdentityService _identityService;

    public GetTeacherUtilizationQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser, IIdentityService identityService)
    {
        _context = context;
        _currentUser = currentUser;
        _identityService = identityService;
    }

    public async Task<List<TeacherUtilizationDto>> Handle(GetTeacherUtilizationQuery request, CancellationToken cancellationToken)
    {
        AnalyticsAccess.EnsureAccess(_currentUser, request.BranchId);

        var teacherIds = request.BranchId.HasValue
            ? await _context.TeacherBranches.Where(tb => tb.BranchId == request.BranchId.Value).Select(tb => tb.TeacherId).Distinct().ToListAsync(cancellationToken)
            : await _context.Teachers.Select(t => t.Id).ToListAsync(cancellationToken);

        // Availability is a weekly recurring pattern, not date-bound, so "available hours over this
        // period" is approximated as weekly available hours times the number of weeks in the period.
        var numberOfWeeks = (request.PeriodEnd.DayNumber - request.PeriodStart.DayNumber + 1) / 7.0;

        var availabilityRows = await _context.TeacherAvailabilities
            .Where(a => teacherIds.Contains(a.TeacherId) && (!request.BranchId.HasValue || a.BranchId == request.BranchId.Value))
            .Select(a => new { a.TeacherId, a.StartTime, a.EndTime })
            .ToListAsync(cancellationToken);

        var weeklyHoursByTeacher = availabilityRows
            .GroupBy(a => a.TeacherId)
            .ToDictionary(g => g.Key, g => g.Sum(a => (a.EndTime.ToTimeSpan() - a.StartTime.ToTimeSpan()).TotalHours));

        var periodStartUtc = request.PeriodStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var periodEndExclusiveUtc = request.PeriodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var sessionRows = await _context.CourseSessions
            .Where(s => teacherIds.Contains(s.TeacherId)
                && s.Status != SessionStatus.Cancelled
                && s.StartUtc >= periodStartUtc && s.StartUtc < periodEndExclusiveUtc
                && (!request.BranchId.HasValue || s.Course.BranchId == request.BranchId.Value))
            .Select(s => new { s.TeacherId, s.StartUtc, s.EndUtc })
            .ToListAsync(cancellationToken);

        var scheduledHoursByTeacher = sessionRows
            .GroupBy(s => s.TeacherId)
            .ToDictionary(g => g.Key, g => g.Sum(s => (s.EndUtc - s.StartUtc).TotalHours));

        var teacherUserIds = await _context.Teachers
            .Where(t => teacherIds.Contains(t.Id))
            .Select(t => new { t.Id, t.UserId })
            .ToListAsync(cancellationToken);

        var fullNameByTeacher = new Dictionary<Guid, string>();
        foreach (var teacher in teacherUserIds)
        {
            var user = await _identityService.GetAuthenticatedUserAsync(teacher.UserId);
            fullNameByTeacher[teacher.Id] = user.FullName;
        }

        return teacherIds.Select(teacherId =>
        {
            var available = weeklyHoursByTeacher.GetValueOrDefault(teacherId, 0) * numberOfWeeks;
            var scheduled = scheduledHoursByTeacher.GetValueOrDefault(teacherId, 0);
            var rate = available > 0 ? scheduled / available : 0;
            var fullName = fullNameByTeacher.GetValueOrDefault(teacherId, "Unknown teacher");
            return new TeacherUtilizationDto(teacherId, fullName, scheduled, available, rate);
        }).ToList();
    }
}

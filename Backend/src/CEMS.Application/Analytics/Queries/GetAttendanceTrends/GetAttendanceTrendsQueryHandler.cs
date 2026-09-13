using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Analytics.Queries.GetAttendanceTrends;

public class GetAttendanceTrendsQueryHandler : IRequestHandler<GetAttendanceTrendsQuery, AttendanceTrendsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAttendanceTrendsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<AttendanceTrendsDto> Handle(GetAttendanceTrendsQuery request, CancellationToken cancellationToken)
    {
        AnalyticsAccess.EnsureAccess(_currentUser, request.BranchId);

        var periodStartUtc = request.PeriodStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var periodEndExclusiveUtc = request.PeriodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var records = _context.SessionAttendances
            .Where(a => a.CourseSession.StartUtc >= periodStartUtc && a.CourseSession.StartUtc < periodEndExclusiveUtc);

        if (request.BranchId.HasValue)
        {
            records = records.Where(a => a.CourseSession.Course.BranchId == request.BranchId.Value);
        }

        var statusCounts = await records
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var presentCount = statusCounts.FirstOrDefault(s => s.Status == AttendanceStatus.Present)?.Count ?? 0;
        var absentCount = statusCounts.FirstOrDefault(s => s.Status == AttendanceStatus.Absent)?.Count ?? 0;
        var lateCount = statusCounts.FirstOrDefault(s => s.Status == AttendanceStatus.Late)?.Count ?? 0;
        var excusedCount = statusCounts.FirstOrDefault(s => s.Status == AttendanceStatus.Excused)?.Count ?? 0;
        var totalRecords = statusCounts.Sum(s => s.Count);
        var attendanceRate = totalRecords > 0 ? (double)presentCount / totalRecords : 0;

        return new AttendanceTrendsDto(totalRecords, presentCount, absentCount, lateCount, excusedCount, attendanceRate);
    }
}

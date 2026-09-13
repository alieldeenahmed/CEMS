using MediatR;

namespace CEMS.Application.Analytics.Queries.GetAttendanceTrends;

public record GetAttendanceTrendsQuery(Guid? BranchId, DateOnly PeriodStart, DateOnly PeriodEnd) : IRequest<AttendanceTrendsDto>;

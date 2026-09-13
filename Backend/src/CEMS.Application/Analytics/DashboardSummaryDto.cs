namespace CEMS.Application.Analytics;

public record DashboardSummaryDto(
    Guid? BranchId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    RevenueSummaryDto Revenue,
    List<TeacherUtilizationDto> TeacherUtilization,
    AttendanceTrendsDto AttendanceTrends,
    EnrollmentFunnelDto EnrollmentFunnel);

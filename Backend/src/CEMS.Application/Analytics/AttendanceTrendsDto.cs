namespace CEMS.Application.Analytics;

public record AttendanceTrendsDto(
    int TotalRecords,
    int PresentCount,
    int AbsentCount,
    int LateCount,
    int ExcusedCount,
    double AttendanceRate);

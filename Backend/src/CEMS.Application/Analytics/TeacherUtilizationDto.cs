namespace CEMS.Application.Analytics;

public record TeacherUtilizationDto(Guid TeacherId, double ScheduledHours, double AvailableHours, double UtilizationRate);

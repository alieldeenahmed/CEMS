namespace CEMS.Application.Analytics;

public record TeacherUtilizationDto(Guid TeacherId, string TeacherFullName, double ScheduledHours, double AvailableHours, double UtilizationRate);

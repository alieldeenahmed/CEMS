namespace CEMS.Application.Teachers;

public record TeacherAvailabilityDto(Guid Id, Guid TeacherId, Guid BranchId, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

using MediatR;

namespace CEMS.Application.Teachers.Commands.AddAvailability;

public record AddAvailabilityCommand(Guid TeacherId, Guid BranchId, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime) : IRequest<TeacherAvailabilityDto>;

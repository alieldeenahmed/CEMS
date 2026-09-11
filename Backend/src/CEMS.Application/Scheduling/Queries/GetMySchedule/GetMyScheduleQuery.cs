using MediatR;

namespace CEMS.Application.Scheduling.Queries.GetMySchedule;

public record GetMyScheduleQuery : IRequest<List<CourseSessionDto>>;

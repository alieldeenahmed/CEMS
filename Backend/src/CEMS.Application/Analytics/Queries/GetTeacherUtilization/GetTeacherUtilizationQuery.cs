using MediatR;

namespace CEMS.Application.Analytics.Queries.GetTeacherUtilization;

public record GetTeacherUtilizationQuery(Guid? BranchId, DateOnly PeriodStart, DateOnly PeriodEnd) : IRequest<List<TeacherUtilizationDto>>;

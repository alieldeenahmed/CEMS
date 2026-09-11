using MediatR;

namespace CEMS.Application.Teachers.Queries.GetAvailabilityForTeacher;

public record GetAvailabilityForTeacherQuery(Guid TeacherId) : IRequest<List<TeacherAvailabilityDto>>;

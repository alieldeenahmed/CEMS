using MediatR;

namespace CEMS.Application.Teachers.Queries.GetQualificationsForTeacher;

public record GetQualificationsForTeacherQuery(Guid TeacherId) : IRequest<List<TeacherCourseQualificationDto>>;

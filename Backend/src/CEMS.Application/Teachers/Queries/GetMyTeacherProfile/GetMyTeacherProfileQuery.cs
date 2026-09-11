using MediatR;

namespace CEMS.Application.Teachers.Queries.GetMyTeacherProfile;

public record GetMyTeacherProfileQuery : IRequest<TeacherDto>;

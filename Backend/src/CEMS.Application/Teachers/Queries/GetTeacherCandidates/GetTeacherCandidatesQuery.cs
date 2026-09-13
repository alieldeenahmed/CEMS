using MediatR;

namespace CEMS.Application.Teachers.Queries.GetTeacherCandidates;

public record GetTeacherCandidatesQuery : IRequest<List<TeacherCandidateDto>>;

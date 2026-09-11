using MediatR;

namespace CEMS.Application.Courses.Queries.GetSubjects;

public record GetSubjectsQuery(Guid? CurriculumId) : IRequest<List<SubjectDto>>;

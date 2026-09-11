using MediatR;

namespace CEMS.Application.Exams.Queries.GetGradesForStudent;

public record GetGradesForStudentQuery(Guid StudentId) : IRequest<List<GradeDto>>;

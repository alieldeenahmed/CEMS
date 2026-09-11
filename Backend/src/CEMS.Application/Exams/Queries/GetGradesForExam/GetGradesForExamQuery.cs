using MediatR;

namespace CEMS.Application.Exams.Queries.GetGradesForExam;

public record GetGradesForExamQuery(Guid ExamId) : IRequest<List<GradeDto>>;

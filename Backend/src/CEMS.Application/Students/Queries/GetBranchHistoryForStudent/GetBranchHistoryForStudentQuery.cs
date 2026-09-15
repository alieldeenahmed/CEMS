using MediatR;

namespace CEMS.Application.Students.Queries.GetBranchHistoryForStudent;

public record GetBranchHistoryForStudentQuery(Guid StudentId) : IRequest<List<StudentBranchHistoryDto>>;

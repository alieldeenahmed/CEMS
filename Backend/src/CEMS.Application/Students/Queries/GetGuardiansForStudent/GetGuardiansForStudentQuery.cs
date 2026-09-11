using CEMS.Application.Students;
using MediatR;

namespace CEMS.Application.Students.Queries.GetGuardiansForStudent;

public record GetGuardiansForStudentQuery(Guid StudentId) : IRequest<List<GuardianDto>>;

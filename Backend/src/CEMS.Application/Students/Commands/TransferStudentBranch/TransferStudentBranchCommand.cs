using MediatR;

namespace CEMS.Application.Students.Commands.TransferStudentBranch;

public record TransferStudentBranchCommand(Guid StudentId, Guid NewBranchId, string? Reason) : IRequest<StudentDto>;

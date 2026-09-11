using MediatR;

namespace CEMS.Application.Teachers.Commands.RemoveTeacherFromBranch;

public record RemoveTeacherFromBranchCommand(Guid TeacherId, Guid BranchId) : IRequest;

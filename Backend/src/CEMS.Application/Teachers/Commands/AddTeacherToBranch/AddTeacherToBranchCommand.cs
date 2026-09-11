using MediatR;

namespace CEMS.Application.Teachers.Commands.AddTeacherToBranch;

public record AddTeacherToBranchCommand(Guid TeacherId, Guid BranchId) : IRequest;

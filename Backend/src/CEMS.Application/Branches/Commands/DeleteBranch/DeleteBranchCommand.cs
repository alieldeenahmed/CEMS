using MediatR;

namespace CEMS.Application.Branches.Commands.DeleteBranch;

public record DeleteBranchCommand(Guid Id) : IRequest;

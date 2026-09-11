using MediatR;

namespace CEMS.Application.Branches.Commands.CreateBranch;

public record CreateBranchCommand(string Name, string Address, string Phone) : IRequest<BranchDto>;

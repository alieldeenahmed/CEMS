using MediatR;

namespace CEMS.Application.Branches.Commands.UpdateBranch;

public record UpdateBranchCommand(Guid Id, string Name, string Address, string Phone, bool IsActive) : IRequest<BranchDto>;

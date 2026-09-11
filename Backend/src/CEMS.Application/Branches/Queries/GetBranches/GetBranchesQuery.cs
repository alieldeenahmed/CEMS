using MediatR;

namespace CEMS.Application.Branches.Queries.GetBranches;

public record GetBranchesQuery : IRequest<List<BranchDto>>;

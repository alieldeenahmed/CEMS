using MediatR;

namespace CEMS.Application.Branches.Queries.GetBranchById;

public record GetBranchByIdQuery(Guid Id) : IRequest<BranchDto>;

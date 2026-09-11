using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Branches;
using MediatR;

namespace CEMS.Application.Branches.Commands.CreateBranch;

public class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, BranchDto>
{
    private readonly IApplicationDbContext _context;

    public CreateBranchCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BranchDto> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Address = request.Address,
            Phone = request.Phone,
            IsActive = true
        };

        _context.Branches.Add(branch);
        await _context.SaveChangesAsync(cancellationToken);

        return new BranchDto(branch.Id, branch.Name, branch.Address, branch.Phone, branch.IsActive);
    }
}

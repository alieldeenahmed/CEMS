using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Branches;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Branches.Commands.DeleteBranch;

public class DeleteBranchCommandHandler : IRequestHandler<DeleteBranchCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteBranchCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Branch), request.Id);

        _context.Branches.Remove(branch);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new BadRequestException(new[] { "Cannot delete a branch that still has rooms assigned to it." });
        }
    }
}

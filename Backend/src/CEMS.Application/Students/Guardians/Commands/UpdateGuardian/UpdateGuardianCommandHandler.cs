using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Application.Students;
using CEMS.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Students.Guardians.Commands.UpdateGuardian;

public class UpdateGuardianCommandHandler : IRequestHandler<UpdateGuardianCommand, GuardianDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateGuardianCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GuardianDto> Handle(UpdateGuardianCommand request, CancellationToken cancellationToken)
    {
        var guardian = await _context.Guardians.FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Guardian), request.Id);

        guardian.FullName = request.FullName;
        guardian.Phone = request.Phone;
        guardian.Email = request.Email;

        await _context.SaveChangesAsync(cancellationToken);

        return GuardianDto.FromEntity(guardian);
    }
}

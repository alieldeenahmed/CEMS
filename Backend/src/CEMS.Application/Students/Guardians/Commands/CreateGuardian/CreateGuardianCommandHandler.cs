using CEMS.Application.Common.Interfaces;
using CEMS.Application.Students;
using CEMS.Domain.Students;
using MediatR;

namespace CEMS.Application.Students.Guardians.Commands.CreateGuardian;

public class CreateGuardianCommandHandler : IRequestHandler<CreateGuardianCommand, GuardianDto>
{
    private readonly IApplicationDbContext _context;

    public CreateGuardianCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GuardianDto> Handle(CreateGuardianCommand request, CancellationToken cancellationToken)
    {
        var guardian = new Guardian
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Phone = request.Phone,
            Email = request.Email,
            UserId = null
        };

        _context.Guardians.Add(guardian);
        await _context.SaveChangesAsync(cancellationToken);

        return GuardianDto.FromEntity(guardian);
    }
}

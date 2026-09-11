using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Commands.UpdateCurriculum;

public class UpdateCurriculumCommandHandler : IRequestHandler<UpdateCurriculumCommand, CurriculumDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateCurriculumCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CurriculumDto> Handle(UpdateCurriculumCommand request, CancellationToken cancellationToken)
    {
        var curriculum = await _context.Curricula.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Curriculum), request.Id);

        curriculum.Name = request.Name;
        curriculum.Description = request.Description;

        await _context.SaveChangesAsync(cancellationToken);

        return new CurriculumDto(curriculum.Id, curriculum.Name, curriculum.Description);
    }
}

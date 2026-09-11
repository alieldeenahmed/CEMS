using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Commands.DeleteCurriculum;

public class DeleteCurriculumCommandHandler : IRequestHandler<DeleteCurriculumCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteCurriculumCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteCurriculumCommand request, CancellationToken cancellationToken)
    {
        var curriculum = await _context.Curricula.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Curriculum), request.Id);

        _context.Curricula.Remove(curriculum);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new BadRequestException(new[] { "Cannot delete a curriculum that still has subjects." });
        }
    }
}

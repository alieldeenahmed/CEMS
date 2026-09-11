using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;

namespace CEMS.Application.Courses.Commands.CreateCurriculum;

public class CreateCurriculumCommandHandler : IRequestHandler<CreateCurriculumCommand, CurriculumDto>
{
    private readonly IApplicationDbContext _context;

    public CreateCurriculumCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CurriculumDto> Handle(CreateCurriculumCommand request, CancellationToken cancellationToken)
    {
        var curriculum = new Curriculum
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description
        };

        _context.Curricula.Add(curriculum);
        await _context.SaveChangesAsync(cancellationToken);

        return new CurriculumDto(curriculum.Id, curriculum.Name, curriculum.Description);
    }
}

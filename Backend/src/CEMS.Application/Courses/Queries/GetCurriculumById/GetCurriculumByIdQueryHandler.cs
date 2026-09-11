using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Queries.GetCurriculumById;

public class GetCurriculumByIdQueryHandler : IRequestHandler<GetCurriculumByIdQuery, CurriculumDto>
{
    private readonly IApplicationDbContext _context;

    public GetCurriculumByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CurriculumDto> Handle(GetCurriculumByIdQuery request, CancellationToken cancellationToken)
    {
        var curriculum = await _context.Curricula.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Curriculum), request.Id);

        return new CurriculumDto(curriculum.Id, curriculum.Name, curriculum.Description);
    }
}

using CEMS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Queries.GetCurricula;

public class GetCurriculaQueryHandler : IRequestHandler<GetCurriculaQuery, List<CurriculumDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCurriculaQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CurriculumDto>> Handle(GetCurriculaQuery request, CancellationToken cancellationToken)
    {
        return await _context.Curricula
            .OrderBy(c => c.Name)
            .Select(c => new CurriculumDto(c.Id, c.Name, c.Description))
            .ToListAsync(cancellationToken);
    }
}

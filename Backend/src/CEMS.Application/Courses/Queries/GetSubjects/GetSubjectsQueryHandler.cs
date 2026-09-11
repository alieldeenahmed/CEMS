using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Queries.GetSubjects;

public class GetSubjectsQueryHandler : IRequestHandler<GetSubjectsQuery, List<SubjectDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSubjectsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SubjectDto>> Handle(GetSubjectsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Subject> query = _context.Subjects;

        if (request.CurriculumId.HasValue)
        {
            query = query.Where(s => s.CurriculumId == request.CurriculumId.Value);
        }

        return await query
            .OrderBy(s => s.Name)
            .Select(s => new SubjectDto(s.Id, s.Name, s.CurriculumId))
            .ToListAsync(cancellationToken);
    }
}

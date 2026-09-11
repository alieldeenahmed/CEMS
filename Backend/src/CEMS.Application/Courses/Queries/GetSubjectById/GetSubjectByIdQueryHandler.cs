using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Queries.GetSubjectById;

public class GetSubjectByIdQueryHandler : IRequestHandler<GetSubjectByIdQuery, SubjectDto>
{
    private readonly IApplicationDbContext _context;

    public GetSubjectByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SubjectDto> Handle(GetSubjectByIdQuery request, CancellationToken cancellationToken)
    {
        var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Subject), request.Id);

        return new SubjectDto(subject.Id, subject.Name, subject.CurriculumId);
    }
}

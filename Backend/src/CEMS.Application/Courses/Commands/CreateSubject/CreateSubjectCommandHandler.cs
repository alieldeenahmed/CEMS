using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Commands.CreateSubject;

public class CreateSubjectCommandHandler : IRequestHandler<CreateSubjectCommand, SubjectDto>
{
    private readonly IApplicationDbContext _context;

    public CreateSubjectCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SubjectDto> Handle(CreateSubjectCommand request, CancellationToken cancellationToken)
    {
        var curriculumExists = await _context.Curricula.AnyAsync(c => c.Id == request.CurriculumId, cancellationToken);
        if (!curriculumExists)
        {
            throw new NotFoundException(nameof(Curriculum), request.CurriculumId);
        }

        var subject = new Subject
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            CurriculumId = request.CurriculumId
        };

        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync(cancellationToken);

        return new SubjectDto(subject.Id, subject.Name, subject.CurriculumId);
    }
}

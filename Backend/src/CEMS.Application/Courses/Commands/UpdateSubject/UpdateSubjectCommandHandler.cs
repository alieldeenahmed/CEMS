using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Commands.UpdateSubject;

public class UpdateSubjectCommandHandler : IRequestHandler<UpdateSubjectCommand, SubjectDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateSubjectCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SubjectDto> Handle(UpdateSubjectCommand request, CancellationToken cancellationToken)
    {
        var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Subject), request.Id);

        subject.Name = request.Name;

        await _context.SaveChangesAsync(cancellationToken);

        return new SubjectDto(subject.Id, subject.Name, subject.CurriculumId);
    }
}

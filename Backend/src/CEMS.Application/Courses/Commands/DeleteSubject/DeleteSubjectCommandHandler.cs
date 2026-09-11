using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Commands.DeleteSubject;

public class DeleteSubjectCommandHandler : IRequestHandler<DeleteSubjectCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteSubjectCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteSubjectCommand request, CancellationToken cancellationToken)
    {
        var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Subject), request.Id);

        _context.Subjects.Remove(subject);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new BadRequestException(new[] { "Cannot delete a subject that still has courses." });
        }
    }
}

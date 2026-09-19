using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Teachers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Teachers.Commands.DeleteTeacher;

public class DeleteTeacherCommandHandler : IRequestHandler<DeleteTeacherCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteTeacherCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteTeacherCommand request, CancellationToken cancellationToken)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Teacher), request.Id);

        // Sessions and payroll runs point at the teacher and must survive them; the database refuses the
        // delete either way, so explain why instead of letting it surface as a 500.
        var hasHistory = await _context.CourseSessions.AnyAsync(s => s.TeacherId == request.Id, cancellationToken)
            || await _context.PayrollRuns.AnyAsync(r => r.TeacherId == request.Id, cancellationToken);

        if (hasHistory)
        {
            throw new BadRequestException(new[] { "Cannot delete a teacher who has scheduled sessions or payroll runs on record." });
        }

        _context.Teachers.Remove(teacher);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

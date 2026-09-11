using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Teachers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Teachers.Commands.UpdateTeacher;

public class UpdateTeacherCommandHandler : IRequestHandler<UpdateTeacherCommand, TeacherDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateTeacherCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TeacherDto> Handle(UpdateTeacherCommand request, CancellationToken cancellationToken)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Teacher), request.Id);

        teacher.HireDate = request.HireDate;
        teacher.PayType = request.PayType;
        teacher.PayRate = request.PayRate;

        await _context.SaveChangesAsync(cancellationToken);

        var branchIds = await _context.TeacherBranches
            .Where(tb => tb.TeacherId == teacher.Id)
            .Select(tb => tb.BranchId)
            .ToListAsync(cancellationToken);

        return new TeacherDto(teacher.Id, teacher.UserId, teacher.HireDate, teacher.PayType, teacher.PayRate, branchIds);
    }
}

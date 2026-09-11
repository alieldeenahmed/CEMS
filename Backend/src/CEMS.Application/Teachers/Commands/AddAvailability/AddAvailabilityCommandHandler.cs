using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Teachers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Teachers.Commands.AddAvailability;

public class AddAvailabilityCommandHandler : IRequestHandler<AddAvailabilityCommand, TeacherAvailabilityDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddAvailabilityCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<TeacherAvailabilityDto> Handle(AddAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == request.TeacherId, cancellationToken)
            ?? throw new NotFoundException(nameof(Teacher), request.TeacherId);

        var isOwnRecord = teacher.UserId == _currentUser.UserId;
        if (!isOwnRecord && !_currentUser.HasAccessToBranch(request.BranchId))
        {
            throw new ForbiddenAccessException("You do not have access to this branch.");
        }

        var isAssignedToBranch = await _context.TeacherBranches
            .AnyAsync(tb => tb.TeacherId == request.TeacherId && tb.BranchId == request.BranchId, cancellationToken);

        if (!isAssignedToBranch)
        {
            throw new BadRequestException(new[] { "This teacher is not assigned to this branch." });
        }

        var availability = new TeacherAvailability
        {
            Id = Guid.NewGuid(),
            TeacherId = request.TeacherId,
            BranchId = request.BranchId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime
        };

        _context.TeacherAvailabilities.Add(availability);
        await _context.SaveChangesAsync(cancellationToken);

        return new TeacherAvailabilityDto(availability.Id, availability.TeacherId, availability.BranchId, availability.DayOfWeek, availability.StartTime, availability.EndTime);
    }
}

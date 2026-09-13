using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Teachers.Commands.CreateTeacher;

public class CreateTeacherCommandHandler : IRequestHandler<CreateTeacherCommand, TeacherDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public CreateTeacherCommandHandler(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<TeacherDto> Handle(CreateTeacherCommand request, CancellationToken cancellationToken)
    {
        AuthenticatedUser user;
        try
        {
            user = await _identityService.GetAuthenticatedUserAsync(request.UserId);
        }
        catch (InvalidOperationException)
        {
            throw new NotFoundException("User", request.UserId);
        }

        if (!user.Roles.Contains(RoleNames.Teacher))
        {
            throw new BadRequestException(new[] { "This user does not have the Teacher role." });
        }

        var alreadyExists = await _context.Teachers.AnyAsync(t => t.UserId == request.UserId, cancellationToken);
        if (alreadyExists)
        {
            throw new BadRequestException(new[] { "A teacher profile already exists for this user." });
        }

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            HireDate = request.HireDate,
            PayType = request.PayType,
            PayRate = request.PayRate
        };

        _context.Teachers.Add(teacher);
        await _context.SaveChangesAsync(cancellationToken);

        return new TeacherDto(teacher.Id, teacher.UserId, user.FullName, user.Email, teacher.HireDate, teacher.PayType, teacher.PayRate, Array.Empty<Guid>());
    }
}

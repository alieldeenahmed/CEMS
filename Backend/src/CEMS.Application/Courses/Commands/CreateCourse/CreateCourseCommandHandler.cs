using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Courses.Commands.CreateCourse;

public class CreateCourseCommandHandler : IRequestHandler<CreateCourseCommand, CourseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateCourseCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CourseDto> Handle(CreateCourseCommand request, CancellationToken cancellationToken)
    {
        var curriculumExists = await _context.Curricula.AnyAsync(c => c.Id == request.CurriculumId, cancellationToken);
        if (!curriculumExists)
        {
            throw new NotFoundException(nameof(Curriculum), request.CurriculumId);
        }

        var branchExists = await _context.Branches.AnyAsync(b => b.Id == request.BranchId, cancellationToken);
        if (!branchExists)
        {
            throw new NotFoundException(nameof(Branch), request.BranchId);
        }

        _currentUser.EnsureAccessToBranch(request.BranchId);

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            DeliveryMode = request.DeliveryMode,
            CurriculumId = request.CurriculumId,
            BranchId = request.BranchId
        };

        _context.Courses.Add(course);
        await _context.SaveChangesAsync(cancellationToken);

        return new CourseDto(course.Id, course.Name, course.DeliveryMode, course.CurriculumId, course.BranchId);
    }
}

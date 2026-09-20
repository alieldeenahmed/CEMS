using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Domain.Courses;
using CEMS.Domain.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Payments.Commands.CreatePackage;

public class CreatePackageCommandHandler : IRequestHandler<CreatePackageCommand, PackageDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreatePackageCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PackageDto> Handle(CreatePackageCommand request, CancellationToken cancellationToken)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Course), request.CourseId);

        _currentUser.EnsureAccessToBranch(course.BranchId);

        var package = new Package
        {
            Id = Guid.NewGuid(),
            CourseId = request.CourseId,
            SessionCount = request.SessionCount,
            Price = request.Price
        };

        _context.Packages.Add(package);
        await _context.SaveChangesAsync(cancellationToken);

        return new PackageDto(package.Id, package.CourseId, package.SessionCount, package.Price);
    }
}

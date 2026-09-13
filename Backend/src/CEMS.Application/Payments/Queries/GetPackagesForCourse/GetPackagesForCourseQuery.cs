using MediatR;

namespace CEMS.Application.Payments.Queries.GetPackagesForCourse;

public record GetPackagesForCourseQuery(Guid CourseId) : IRequest<List<PackageDto>>;

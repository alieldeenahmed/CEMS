using CEMS.Domain.Courses;
using MediatR;

namespace CEMS.Application.Courses.Commands.CreateCourse;

public record CreateCourseCommand(string Name, DeliveryMode DeliveryMode, Guid SubjectId, Guid BranchId) : IRequest<CourseDto>;

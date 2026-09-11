using CEMS.Domain.Courses;
using MediatR;

namespace CEMS.Application.Courses.Commands.UpdateCourse;

public record UpdateCourseCommand(Guid Id, string Name, DeliveryMode DeliveryMode) : IRequest<CourseDto>;

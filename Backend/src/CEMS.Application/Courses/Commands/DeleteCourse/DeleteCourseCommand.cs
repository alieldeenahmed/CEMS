using MediatR;

namespace CEMS.Application.Courses.Commands.DeleteCourse;

public record DeleteCourseCommand(Guid Id) : IRequest;

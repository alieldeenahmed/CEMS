using MediatR;

namespace CEMS.Application.Teachers.Commands.AddTeacherQualification;

public record AddTeacherQualificationCommand(Guid TeacherId, Guid CourseId) : IRequest;

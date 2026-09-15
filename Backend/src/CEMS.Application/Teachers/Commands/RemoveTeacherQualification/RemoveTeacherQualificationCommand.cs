using MediatR;

namespace CEMS.Application.Teachers.Commands.RemoveTeacherQualification;

public record RemoveTeacherQualificationCommand(Guid TeacherId, Guid CourseId) : IRequest;

using MediatR;

namespace CEMS.Application.Scheduling.Commands.SubstituteSessionTeacher;

public record SubstituteSessionTeacherCommand(Guid SessionId, Guid NewTeacherId, bool Override, string? OverrideReason) : IRequest<CourseSessionDto>;

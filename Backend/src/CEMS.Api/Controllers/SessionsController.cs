using CEMS.Application.Attendance;
using CEMS.Application.Attendance.Commands.MarkAttendance;
using CEMS.Application.Attendance.Queries.GetAttendanceForSession;
using CEMS.Application.Scheduling;
using CEMS.Application.Scheduling.Commands.CancelSession;
using CEMS.Application.Scheduling.Commands.CreateSession;
using CEMS.Application.Scheduling.Commands.SubstituteSessionTeacher;
using CEMS.Application.Scheduling.Queries.GetSessionById;
using CEMS.Application.Scheduling.Queries.GetSessionsForCourse;
using CEMS.Domain.Attendance;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Controllers;

[ApiController]
public class SessionsController : ControllerBase
{
    private const string StaffRoles = RoleNames.Owner + "," + RoleNames.BranchManager + "," + RoleNames.FrontDesk;
    private const string ViewRoles = StaffRoles + "," + RoleNames.Teacher;
    private const string MarkAttendanceRoles = RoleNames.Owner + "," + RoleNames.BranchManager + "," + RoleNames.Teacher;

    private readonly IMediator _mediator;

    public SessionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("api/courses/{courseId:guid}/sessions")]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<List<CourseSessionDto>>> GetSessionsForCourse(Guid courseId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSessionsForCourseQuery(courseId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("api/courses/{courseId:guid}/sessions")]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<CourseSessionDto>> CreateSession(Guid courseId, CreateSessionRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateSessionCommand(courseId, request.RoomId, request.TeacherId, request.StartUtc, request.EndUtc, request.Override, request.OverrideReason);
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetSessionById), new { id = result.Id }, result);
    }

    [HttpGet("api/sessions/{id:guid}")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<CourseSessionDto>> GetSessionById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSessionByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("api/sessions/{id:guid}/cancel")]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<CourseSessionDto>> CancelSession(Guid id, CancelSessionRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CancelSessionCommand(id, request.RescheduledToSessionId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("api/sessions/{id:guid}/substitute-teacher")]
    [Authorize(Roles = StaffRoles)]
    public async Task<ActionResult<CourseSessionDto>> SubstituteTeacher(Guid id, SubstituteTeacherRequest request, CancellationToken cancellationToken)
    {
        var command = new SubstituteSessionTeacherCommand(id, request.NewTeacherId, request.Override, request.OverrideReason);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpGet("api/sessions/{id:guid}/attendance")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<List<AttendanceRecordDto>>> GetAttendance(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAttendanceForSessionQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("api/sessions/{id:guid}/attendance")]
    [Authorize(Roles = MarkAttendanceRoles)]
    public async Task<ActionResult<AttendanceRecordDto>> MarkAttendance(Guid id, MarkAttendanceRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new MarkAttendanceCommand(id, request.StudentId, request.Status), cancellationToken);
        return Ok(result);
    }
}

public record CreateSessionRequest(Guid RoomId, Guid TeacherId, DateTime StartUtc, DateTime EndUtc, bool Override, string? OverrideReason);
public record CancelSessionRequest(Guid? RescheduledToSessionId);
public record SubstituteTeacherRequest(Guid NewTeacherId, bool Override, string? OverrideReason);
public record MarkAttendanceRequest(Guid StudentId, AttendanceStatus Status);

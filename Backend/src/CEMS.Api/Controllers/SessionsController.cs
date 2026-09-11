using CEMS.Application.Scheduling;
using CEMS.Application.Scheduling.Commands.CancelSession;
using CEMS.Application.Scheduling.Commands.CreateSession;
using CEMS.Application.Scheduling.Queries.GetSessionById;
using CEMS.Application.Scheduling.Queries.GetSessionsForCourse;
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
}

public record CreateSessionRequest(Guid RoomId, Guid TeacherId, DateTime StartUtc, DateTime EndUtc, bool Override, string? OverrideReason);
public record CancelSessionRequest(Guid? RescheduledToSessionId);

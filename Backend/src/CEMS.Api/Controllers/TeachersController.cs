using CEMS.Application.Teachers;
using CEMS.Application.Teachers.Commands.AddAvailability;
using CEMS.Application.Teachers.Commands.AddTeacherToBranch;
using CEMS.Application.Teachers.Commands.CreateTeacher;
using CEMS.Application.Teachers.Commands.DeleteTeacher;
using CEMS.Application.Teachers.Commands.RemoveAvailability;
using CEMS.Application.Teachers.Commands.RemoveTeacherFromBranch;
using CEMS.Application.Teachers.Commands.UpdateTeacher;
using CEMS.Application.Scheduling;
using CEMS.Application.Scheduling.Queries.GetMySchedule;
using CEMS.Application.Teachers.Queries.GetAvailabilityForTeacher;
using CEMS.Application.Teachers.Queries.GetMyTeacherProfile;
using CEMS.Application.Teachers.Queries.GetTeacherById;
using CEMS.Application.Teachers.Queries.GetTeachers;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Controllers;

[ApiController]
[Route("api/teachers")]
public class TeachersController : ControllerBase
{
    private const string ViewRoles = RoleNames.Owner + "," + RoleNames.BranchManager + "," + RoleNames.FrontDesk + "," + RoleNames.Teacher;
    private const string BranchManageRoles = RoleNames.Owner + "," + RoleNames.BranchManager;
    private const string AvailabilityRoles = RoleNames.Owner + "," + RoleNames.BranchManager + "," + RoleNames.Teacher;

    private readonly IMediator _mediator;

    public TeachersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.Owner + "," + RoleNames.BranchManager + "," + RoleNames.FrontDesk)]
    public async Task<ActionResult<List<TeacherDto>>> GetTeachers(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetTeachersQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("my-profile")]
    [Authorize(Roles = RoleNames.Teacher)]
    public async Task<ActionResult<TeacherDto>> GetMyProfile(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMyTeacherProfileQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("my-schedule")]
    [Authorize(Roles = RoleNames.Teacher)]
    public async Task<ActionResult<List<CourseSessionDto>>> GetMySchedule(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMyScheduleQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<TeacherDto>> GetTeacherById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetTeacherByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<TeacherDto>> CreateTeacher(CreateTeacherCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetTeacherById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<TeacherDto>> UpdateTeacher(Guid id, UpdateTeacherRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateTeacherCommand(id, request.HireDate, request.PayType, request.PayRate);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<IActionResult> DeleteTeacher(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteTeacherCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/branches")]
    [Authorize(Roles = BranchManageRoles)]
    public async Task<IActionResult> AddTeacherToBranch(Guid id, AddTeacherToBranchRequest request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new AddTeacherToBranchCommand(id, request.BranchId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/branches/{branchId:guid}")]
    [Authorize(Roles = BranchManageRoles)]
    public async Task<IActionResult> RemoveTeacherFromBranch(Guid id, Guid branchId, CancellationToken cancellationToken)
    {
        await _mediator.Send(new RemoveTeacherFromBranchCommand(id, branchId), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/availability")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<List<TeacherAvailabilityDto>>> GetAvailability(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAvailabilityForTeacherQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/availability")]
    [Authorize(Roles = AvailabilityRoles)]
    public async Task<ActionResult<TeacherAvailabilityDto>> AddAvailability(Guid id, AddAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var command = new AddAvailabilityCommand(id, request.BranchId, request.DayOfWeek, request.StartTime, request.EndTime);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("availability/{id:guid}")]
    [Authorize(Roles = AvailabilityRoles)]
    public async Task<IActionResult> RemoveAvailability(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new RemoveAvailabilityCommand(id), cancellationToken);
        return NoContent();
    }
}

public record UpdateTeacherRequest(DateOnly HireDate, PayType PayType, decimal PayRate);
public record AddTeacherToBranchRequest(Guid BranchId);
public record AddAvailabilityRequest(Guid BranchId, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

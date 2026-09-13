using CEMS.Application.Payroll;
using CEMS.Application.Payroll.Commands.GenerateStaffPayrollRun;
using CEMS.Application.Payroll.Queries.GetMyStaffPayrollRuns;
using CEMS.Application.Payroll.Queries.GetStaffPayrollRunsForUser;
using CEMS.Application.Users;
using CEMS.Application.Users.Commands.CreateStaffUser;
using CEMS.Application.Users.Commands.SetStaffUserActive;
using CEMS.Application.Users.Queries.GetStaffUsers;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private const string NonTeachingStaffRoles = RoleNames.FrontDesk + "," + RoleNames.BranchManager;

    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("staff")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<List<StaffUserDto>>> GetStaffUsers(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetStaffUsersQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("staff")]
    [Authorize(Roles = RoleNames.Owner + "," + RoleNames.BranchManager)]
    public async Task<ActionResult<StaffUserDto>> CreateStaffUser(CreateStaffUserCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPut("staff/{id:guid}/status")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<IActionResult> SetStaffUserActive(Guid id, SetStaffUserActiveRequest request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new SetStaffUserActiveCommand(id, request.IsActive), cancellationToken);
        return NoContent();
    }

    // Front Desk and Branch Manager work isn't tracked in sessions like a Teacher's, so their
    // payroll runs carry a manually-entered amount instead of an auto-computed one.
    [HttpGet("staff/{id:guid}/payroll-runs")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<List<StaffPayrollRunDto>>> GetStaffPayrollRuns(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetStaffPayrollRunsForUserQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("staff/{id:guid}/payroll-runs")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<StaffPayrollRunDto>> GenerateStaffPayrollRun(Guid id, GenerateStaffPayrollRunRequest request, CancellationToken cancellationToken)
    {
        var command = new GenerateStaffPayrollRunCommand(id, request.PeriodStart, request.PeriodEnd, request.Amount);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpGet("my-staff-payroll-runs")]
    [Authorize(Roles = NonTeachingStaffRoles)]
    public async Task<ActionResult<List<StaffPayrollRunDto>>> GetMyStaffPayrollRuns(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMyStaffPayrollRunsQuery(), cancellationToken);
        return Ok(result);
    }
}

public record GenerateStaffPayrollRunRequest(DateOnly PeriodStart, DateOnly PeriodEnd, decimal Amount);

public record SetStaffUserActiveRequest(bool IsActive);

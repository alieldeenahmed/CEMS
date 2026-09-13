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
}

public record SetStaffUserActiveRequest(bool IsActive);

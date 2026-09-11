using CEMS.Application.Users;
using CEMS.Application.Users.Commands.CreateStaffUser;
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

    [HttpPost("staff")]
    [Authorize(Roles = RoleNames.Owner + "," + RoleNames.BranchManager)]
    public async Task<ActionResult<StaffUserDto>> CreateStaffUser(CreateStaffUserCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}

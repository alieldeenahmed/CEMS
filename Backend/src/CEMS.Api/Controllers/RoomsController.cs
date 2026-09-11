using CEMS.Application.Branches;
using CEMS.Application.Branches.Rooms.Commands.CreateRoom;
using CEMS.Application.Branches.Rooms.Commands.DeleteRoom;
using CEMS.Application.Branches.Rooms.Commands.UpdateRoom;
using CEMS.Application.Branches.Rooms.Queries.GetRoomById;
using CEMS.Application.Branches.Rooms.Queries.GetRoomsByBranch;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Controllers;

[ApiController]
public class RoomsController : ControllerBase
{
    private const string ViewRoles = RoleNames.Owner + "," + RoleNames.BranchManager + "," + RoleNames.FrontDesk;
    private const string ManageRoles = RoleNames.Owner + "," + RoleNames.BranchManager;

    private readonly IMediator _mediator;

    public RoomsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("api/branches/{branchId:guid}/rooms")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<List<RoomDto>>> GetRoomsByBranch(Guid branchId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetRoomsByBranchQuery(branchId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("api/branches/{branchId:guid}/rooms")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<RoomDto>> CreateRoom(Guid branchId, CreateRoomRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateRoomCommand(branchId, request.Name, request.Capacity);
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetRoomById), new { id = result.Id }, result);
    }

    [HttpGet("api/rooms/{id:guid}")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<RoomDto>> GetRoomById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetRoomByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPut("api/rooms/{id:guid}")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<RoomDto>> UpdateRoom(Guid id, UpdateRoomRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateRoomCommand(id, request.Name, request.Capacity);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("api/rooms/{id:guid}")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> DeleteRoom(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteRoomCommand(id), cancellationToken);
        return NoContent();
    }
}

public record CreateRoomRequest(string Name, int Capacity);
public record UpdateRoomRequest(string Name, int Capacity);

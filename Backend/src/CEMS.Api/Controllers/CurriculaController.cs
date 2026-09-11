using CEMS.Application.Courses;
using CEMS.Application.Courses.Commands.CreateCurriculum;
using CEMS.Application.Courses.Commands.DeleteCurriculum;
using CEMS.Application.Courses.Commands.UpdateCurriculum;
using CEMS.Application.Courses.Queries.GetCurriculumById;
using CEMS.Application.Courses.Queries.GetCurricula;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Controllers;

[ApiController]
[Route("api/curricula")]
public class CurriculaController : ControllerBase
{
    private readonly IMediator _mediator;

    public CurriculaController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<List<CurriculumDto>>> GetCurricula(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCurriculaQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CurriculumDto>> GetCurriculumById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCurriculumByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<CurriculumDto>> CreateCurriculum(CreateCurriculumCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetCurriculumById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<CurriculumDto>> UpdateCurriculum(Guid id, UpdateCurriculumRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateCurriculumCommand(id, request.Name, request.Description);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<IActionResult> DeleteCurriculum(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteCurriculumCommand(id), cancellationToken);
        return NoContent();
    }
}

public record UpdateCurriculumRequest(string Name, string Description);

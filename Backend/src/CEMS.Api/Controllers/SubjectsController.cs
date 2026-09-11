using CEMS.Application.Courses;
using CEMS.Application.Courses.Commands.CreateSubject;
using CEMS.Application.Courses.Commands.DeleteSubject;
using CEMS.Application.Courses.Commands.UpdateSubject;
using CEMS.Application.Courses.Queries.GetSubjectById;
using CEMS.Application.Courses.Queries.GetSubjects;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Controllers;

[ApiController]
[Route("api/subjects")]
public class SubjectsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SubjectsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<List<SubjectDto>>> GetSubjects([FromQuery] Guid? curriculumId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSubjectsQuery(curriculumId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SubjectDto>> GetSubjectById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSubjectByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<SubjectDto>> CreateSubject(CreateSubjectCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetSubjectById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<SubjectDto>> UpdateSubject(Guid id, UpdateSubjectRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateSubjectCommand(id, request.Name);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<IActionResult> DeleteSubject(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteSubjectCommand(id), cancellationToken);
        return NoContent();
    }
}

public record UpdateSubjectRequest(string Name);

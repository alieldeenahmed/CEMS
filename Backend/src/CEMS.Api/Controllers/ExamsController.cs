using CEMS.Application.Exams;
using CEMS.Application.Exams.Commands.CreateExam;
using CEMS.Application.Exams.Commands.DeleteExam;
using CEMS.Application.Exams.Commands.RecordGrade;
using CEMS.Application.Exams.Commands.UpdateExam;
using CEMS.Application.Exams.Queries.GetExamById;
using CEMS.Application.Exams.Queries.GetExamsForCourse;
using CEMS.Application.Exams.Queries.GetGradesForExam;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Controllers;

[ApiController]
public class ExamsController : ControllerBase
{
    private const string ViewRoles = RoleNames.Owner + "," + RoleNames.BranchManager + "," + RoleNames.FrontDesk + "," + RoleNames.Teacher;
    private const string ManageRoles = RoleNames.Owner + "," + RoleNames.BranchManager + "," + RoleNames.Teacher;

    private readonly IMediator _mediator;

    public ExamsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("api/courses/{courseId:guid}/exams")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<List<ExamDto>>> GetExamsForCourse(Guid courseId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetExamsForCourseQuery(courseId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("api/courses/{courseId:guid}/exams")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<ExamDto>> CreateExam(Guid courseId, CreateExamRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateExamCommand(courseId, request.Name, request.MaxScore, request.ExamDate);
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetExamById), new { id = result.Id }, result);
    }

    [HttpGet("api/exams/{id:guid}")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<ExamDto>> GetExamById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetExamByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPut("api/exams/{id:guid}")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<ExamDto>> UpdateExam(Guid id, UpdateExamRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateExamCommand(id, request.Name, request.MaxScore, request.ExamDate);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("api/exams/{id:guid}")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> DeleteExam(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteExamCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpGet("api/exams/{id:guid}/grades")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<List<GradeDto>>> GetGrades(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetGradesForExamQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("api/exams/{id:guid}/grades")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<GradeDto>> RecordGrade(Guid id, RecordGradeRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RecordGradeCommand(id, request.StudentId, request.Score, request.Comments), cancellationToken);
        return Ok(result);
    }
}

public record CreateExamRequest(string Name, decimal MaxScore, DateOnly ExamDate);
public record UpdateExamRequest(string Name, decimal MaxScore, DateOnly ExamDate);
public record RecordGradeRequest(Guid StudentId, decimal Score, string? Comments);

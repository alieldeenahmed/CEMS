using CEMS.Application.Attendance;
using CEMS.Application.Attendance.Queries.GetAttendanceForStudent;
using CEMS.Application.Courses;
using CEMS.Application.Courses.Queries.GetEnrollmentsForStudent;
using CEMS.Application.Students;
using CEMS.Application.Students.Commands.CreateStudent;
using CEMS.Application.Students.Commands.DeleteStudent;
using CEMS.Application.Students.Commands.LinkGuardian;
using CEMS.Application.Students.Commands.UnlinkGuardian;
using CEMS.Application.Students.Commands.UpdateStudent;
using CEMS.Application.Students.Queries.GetGuardiansForStudent;
using CEMS.Application.Students.Queries.GetMyChildren;
using CEMS.Application.Students.Queries.GetStudentById;
using CEMS.Application.Students.Queries.GetStudents;
using CEMS.Domain.Students;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Controllers;

[ApiController]
[Route("api/students")]
public class StudentsController : ControllerBase
{
    private const string ViewRoles = RoleNames.Owner + "," + RoleNames.BranchManager + "," + RoleNames.FrontDesk + "," + RoleNames.Parent;
    private const string ManageRoles = RoleNames.Owner + "," + RoleNames.BranchManager + "," + RoleNames.FrontDesk;

    private readonly IMediator _mediator;

    public StudentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<List<StudentDto>>> GetStudents(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetStudentsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("my-children")]
    [Authorize(Roles = RoleNames.Parent)]
    public async Task<ActionResult<List<StudentDto>>> GetMyChildren(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMyChildrenQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<StudentDto>> GetStudentById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetStudentByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<StudentDto>> CreateStudent(CreateStudentCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetStudentById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<StudentDto>> UpdateStudent(Guid id, UpdateStudentRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateStudentCommand(id, request.FullName, request.DateOfBirth, request.Gender, request.Status);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> DeleteStudent(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteStudentCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/guardians")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<List<GuardianDto>>> GetGuardiansForStudent(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetGuardiansForStudentQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/guardians")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> LinkGuardian(Guid id, LinkGuardianRequest request, CancellationToken cancellationToken)
    {
        var command = new LinkGuardianCommand(id, request.GuardianId, request.RelationshipType, request.IsPrimaryContact);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/guardians/{guardianId:guid}")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> UnlinkGuardian(Guid id, Guid guardianId, CancellationToken cancellationToken)
    {
        await _mediator.Send(new UnlinkGuardianCommand(id, guardianId), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/enrollments")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<List<CourseEnrollmentDto>>> GetEnrollmentsForStudent(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetEnrollmentsForStudentQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/attendance")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<List<AttendanceRecordDto>>> GetAttendanceForStudent(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAttendanceForStudentQuery(id), cancellationToken);
        return Ok(result);
    }
}

public record UpdateStudentRequest(string FullName, DateOnly DateOfBirth, Gender Gender, StudentStatus Status);
public record LinkGuardianRequest(Guid GuardianId, RelationshipType RelationshipType, bool IsPrimaryContact);

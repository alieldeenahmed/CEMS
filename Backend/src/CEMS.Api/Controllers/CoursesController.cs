using CEMS.Application.Courses;
using CEMS.Application.Courses.Commands.CreateCourse;
using CEMS.Application.Courses.Commands.DeleteCourse;
using CEMS.Application.Courses.Commands.DropEnrollment;
using CEMS.Application.Courses.Commands.EnrollStudent;
using CEMS.Application.Courses.Commands.UpdateCourse;
using CEMS.Application.Courses.Queries.GetCourseById;
using CEMS.Application.Courses.Queries.GetCourses;
using CEMS.Application.Courses.Commands.PromoteFromWaitlist;
using CEMS.Application.Courses.Queries.GetEnrollmentsForCourse;
using CEMS.Application.Courses.Queries.GetMyCourses;
using CEMS.Domain.Courses;
using CEMS.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CEMS.Api.Controllers;

[ApiController]
[Route("api/courses")]
public class CoursesController : ControllerBase
{
    private const string ViewRoles = RoleNames.Owner + "," + RoleNames.BranchManager + "," + RoleNames.FrontDesk;
    private const string ManageRoles = RoleNames.Owner + "," + RoleNames.BranchManager;

    private readonly IMediator _mediator;

    public CoursesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<List<CourseDto>>> GetCourses(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCoursesQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("my-courses")]
    [Authorize(Roles = RoleNames.Teacher)]
    public async Task<ActionResult<List<CourseDto>>> GetMyCourses(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMyCoursesQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<CourseDto>> GetCourseById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCourseByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<CourseDto>> CreateCourse(CreateCourseCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetCourseById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<CourseDto>> UpdateCourse(Guid id, UpdateCourseRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateCourseCommand(id, request.Name, request.DeliveryMode);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> DeleteCourse(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteCourseCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/enrollments")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<List<CourseEnrollmentDto>>> GetEnrollments(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetEnrollmentsForCourseQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/enrollments")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<CourseEnrollmentDto>> EnrollStudent(Guid id, EnrollStudentRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new EnrollStudentCommand(request.StudentId, id), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("enrollments/{enrollmentId:guid}")]
    [Authorize(Roles = ViewRoles)]
    public async Task<IActionResult> DropEnrollment(Guid enrollmentId, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DropEnrollmentCommand(enrollmentId), cancellationToken);
        return NoContent();
    }

    [HttpPost("enrollments/{enrollmentId:guid}/promote")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<CourseEnrollmentDto>> PromoteFromWaitlist(Guid enrollmentId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new PromoteFromWaitlistCommand(enrollmentId), cancellationToken);
        return Ok(result);
    }
}

public record UpdateCourseRequest(string Name, DeliveryMode DeliveryMode);
public record EnrollStudentRequest(Guid StudentId);

using CEMS.Application.Teachers;
using CEMS.Application.Teachers.Commands.AddAvailability;
using CEMS.Application.Teachers.Commands.AddTeacherQualification;
using CEMS.Application.Teachers.Commands.AddTeacherToBranch;
using CEMS.Application.Teachers.Commands.CreateTeacher;
using CEMS.Application.Teachers.Commands.DeleteTeacher;
using CEMS.Application.Teachers.Commands.RemoveAvailability;
using CEMS.Application.Teachers.Commands.RemoveTeacherFromBranch;
using CEMS.Application.Teachers.Commands.RemoveTeacherQualification;
using CEMS.Application.Teachers.Commands.UpdateTeacher;
using CEMS.Application.Payroll;
using CEMS.Application.Payroll.Commands.GeneratePayrollRun;
using CEMS.Application.Payroll.Queries.GetMyPayrollRuns;
using CEMS.Application.Payroll.Queries.GetPayrollRunsForTeacher;
using CEMS.Application.Scheduling;
using CEMS.Application.Scheduling.Queries.GetMySchedule;
using CEMS.Application.Teachers.Queries.GetAvailabilityForTeacher;
using CEMS.Application.Teachers.Queries.GetMyTeacherProfile;
using CEMS.Application.Teachers.Queries.GetQualificationsForTeacher;
using CEMS.Application.Teachers.Queries.GetTeacherById;
using CEMS.Application.Teachers.Queries.GetTeacherCandidates;
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

    [HttpGet("candidates")]
    [Authorize(Roles = BranchManageRoles)]
    public async Task<ActionResult<List<TeacherCandidateDto>>> GetTeacherCandidates(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetTeacherCandidatesQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = BranchManageRoles)]
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

    // Declares which courses a teacher is qualified to teach -- distinct from CourseSession.TeacherId,
    // which tracks what they're actually scheduled for. Nothing in scheduling reads this yet; it's a
    // standalone record for staffing decisions (e.g. picking a substitute) until something needs it.
    [HttpGet("{id:guid}/qualifications")]
    [Authorize(Roles = ViewRoles)]
    public async Task<ActionResult<List<TeacherCourseQualificationDto>>> GetQualifications(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetQualificationsForTeacherQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/qualifications")]
    [Authorize(Roles = BranchManageRoles)]
    public async Task<IActionResult> AddQualification(Guid id, AddQualificationRequest request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new AddTeacherQualificationCommand(id, request.CourseId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/qualifications/{courseId:guid}")]
    [Authorize(Roles = BranchManageRoles)]
    public async Task<IActionResult> RemoveQualification(Guid id, Guid courseId, CancellationToken cancellationToken)
    {
        await _mediator.Send(new RemoveTeacherQualificationCommand(id, courseId), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/payroll-runs")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<List<PayrollRunDto>>> GetPayrollRunsForTeacher(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPayrollRunsForTeacherQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/payroll-runs")]
    [Authorize(Roles = RoleNames.Owner)]
    public async Task<ActionResult<PayrollRunDto>> GeneratePayrollRun(Guid id, GeneratePayrollRunRequest request, CancellationToken cancellationToken)
    {
        var command = new GeneratePayrollRunCommand(id, request.PeriodStart, request.PeriodEnd);
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction("GetPayrollRunById", "PayrollRuns", new { id = result.Id }, result);
    }

    [HttpGet("my-payroll-runs")]
    [Authorize(Roles = RoleNames.Teacher)]
    public async Task<ActionResult<List<PayrollRunDto>>> GetMyPayrollRuns(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMyPayrollRunsQuery(), cancellationToken);
        return Ok(result);
    }
}

public record UpdateTeacherRequest(DateOnly HireDate, PayType PayType, decimal PayRate);
public record AddTeacherToBranchRequest(Guid BranchId);
public record AddAvailabilityRequest(Guid BranchId, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
public record AddQualificationRequest(Guid CourseId);
public record GeneratePayrollRunRequest(DateOnly PeriodStart, DateOnly PeriodEnd);

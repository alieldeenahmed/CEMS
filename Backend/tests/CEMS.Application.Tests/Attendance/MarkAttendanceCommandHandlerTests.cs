using CEMS.Application.Attendance.Commands.MarkAttendance;
using CEMS.Application.Common.Exceptions;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Attendance;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Students;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Attendance;

public class MarkAttendanceCommandHandlerTests : HandlerTestBase
{
    private readonly Guid _teacherUserId = Guid.NewGuid();
    private Guid _sessionId;
    private Guid _studentId;

    private void Seed(DateTime sessionStartUtc, DateTime sessionEndUtc, SessionStatus status = SessionStatus.Scheduled)
    {
        var branch = new Branch { Id = Guid.NewGuid(), Name = "Downtown" };
        var room = new Room { Id = Guid.NewGuid(), Name = "Room A", Capacity = 10, BranchId = branch.Id };
        var teacher = new Teacher { Id = Guid.NewGuid(), UserId = _teacherUserId, HireDate = DateOnly.FromDateTime(DateTime.UtcNow), PayType = PayType.Hourly, PayRate = 100 };
        var curriculum = new Curriculum { Id = Guid.NewGuid(), Name = "IG", Description = "IG" };
        var course = new Course { Id = Guid.NewGuid(), Name = "Math", DeliveryMode = DeliveryMode.Group, CurriculumId = curriculum.Id, BranchId = branch.Id };
        var student = new Student
        {
            Id = Guid.NewGuid(),
            FullName = "Sam Student",
            DateOfBirth = new DateOnly(2013, 1, 1),
            Gender = Gender.Male,
            EnrollmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CurrentBranchId = branch.Id
        };
        var enrollment = new CourseEnrollment { Id = Guid.NewGuid(), StudentId = student.Id, CourseId = course.Id, EnrollmentDate = DateOnly.FromDateTime(DateTime.UtcNow), Status = CourseEnrollmentStatus.Active };
        var session = new CourseSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RoomId = room.Id,
            TeacherId = teacher.Id,
            StartUtc = sessionStartUtc,
            EndUtc = sessionEndUtc,
            Status = status
        };

        Context.AddRange(branch, room, teacher, curriculum, course, student, enrollment, session);
        Context.SaveChanges();

        _sessionId = session.Id;
        _studentId = student.Id;

        CurrentUser.UserId = _teacherUserId;
        CurrentUser.Roles = [RoleNames.Teacher];
    }

    private MarkAttendanceCommandHandler CreateHandler() => new(Context, CurrentUser);

    [Fact]
    public async Task Handle_SessionInFuture_ThrowsBadRequest()
    {
        Seed(DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(3));

        var handler = CreateHandler();
        var command = new MarkAttendanceCommand(_sessionId, _studentId, AttendanceStatus.Present);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains(exception.Errors, e => e.Contains("hasn't started"));
    }

    [Fact]
    public async Task Handle_MoreThanFourHoursAfterSessionEnd_ThrowsBadRequest()
    {
        Seed(DateTime.UtcNow.AddHours(-6), DateTime.UtcNow.AddHours(-5));

        var handler = CreateHandler();
        var command = new MarkAttendanceCommand(_sessionId, _studentId, AttendanceStatus.Present);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains(exception.Errors, e => e.Contains("window"));
    }

    [Fact]
    public async Task Handle_WithinFourHourWindowAfterSessionEnd_Succeeds()
    {
        Seed(DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1));

        var handler = CreateHandler();
        var command = new MarkAttendanceCommand(_sessionId, _studentId, AttendanceStatus.Present);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(AttendanceStatus.Present, result.Status);
    }

    [Fact]
    public async Task Handle_UnrelatedTeacher_ThrowsForbidden()
    {
        Seed(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddMinutes(-30));
        CurrentUser.UserId = Guid.NewGuid(); // a different teacher, not assigned to this session

        var handler = CreateHandler();
        var command = new MarkAttendanceCommand(_sessionId, _studentId, AttendanceStatus.Present);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_BranchManagerWithBranchAccess_CanMarkAttendance()
    {
        Seed(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddMinutes(-30));

        var course = Context.Courses.First();
        CurrentUser.UserId = Guid.NewGuid();
        CurrentUser.Roles = [RoleNames.BranchManager];
        CurrentUser.BranchIds = [course.BranchId];

        var handler = CreateHandler();
        var command = new MarkAttendanceCommand(_sessionId, _studentId, AttendanceStatus.Absent);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(AttendanceStatus.Absent, result.Status);
    }
}

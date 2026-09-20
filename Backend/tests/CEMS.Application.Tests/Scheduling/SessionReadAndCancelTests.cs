using CEMS.Application.Attendance.Commands.MarkAttendance;
using CEMS.Application.Attendance.Queries.GetAttendanceForSession;
using CEMS.Application.Attendance.Queries.GetAttendanceForStudent;
using CEMS.Application.Common.Exceptions;
using CEMS.Application.Payments.Queries.GetInvoiceById;
using CEMS.Application.Payments.Queries.GetPaymentsForInvoice;
using CEMS.Application.Scheduling.Commands.CancelSession;
using CEMS.Application.Scheduling.Queries.GetSessionById;
using CEMS.Application.Scheduling.Queries.GetSessionsForCourse;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Attendance;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Payments;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Scheduling;

public class SessionReadAndCancelTests : SeededHandlerTestBase
{
    private static readonly DateTime FutureStart = new(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc);

    private readonly Branch _smouha;
    private readonly Branch _kafrAbdo;
    private readonly Course _course;
    private readonly Room _room;
    private readonly Teacher _teacher;

    public SessionReadAndCancelTests()
    {
        _smouha = AddBranch("Smouha");
        _kafrAbdo = AddBranch("Kafr Abdo");
        _course = AddCourse(_smouha);
        _room = AddRoom(_smouha);
        _teacher = AddTeacher(_smouha);
    }

    private void ActAsTeacher(Teacher teacher)
    {
        CurrentUser.UserId = teacher.UserId;
        CurrentUser.Roles = [RoleNames.Teacher];
        CurrentUser.BranchIds = [];
    }

    // ---- CancelSession ----

    [Fact]
    public async Task Cancel_MarksTheSessionCancelled_AndCanLinkTheMakeupSession()
    {
        var session = AddSession(_course, _room, _teacher, FutureStart);
        var makeup = AddSession(_course, _room, _teacher, FutureStart.AddDays(7));
        ActAs(RoleNames.BranchManager, _smouha);

        var dto = await new CancelSessionCommandHandler(Context, CurrentUser)
            .Handle(new CancelSessionCommand(session.Id, makeup.Id), CancellationToken.None);

        Assert.Equal(SessionStatus.Cancelled, dto.Status);
        Assert.Equal(makeup.Id, dto.RescheduledToSessionId);
    }

    [Fact]
    public async Task Cancel_WithoutAMakeup_LeavesTheLinkEmpty()
    {
        var session = AddSession(_course, _room, _teacher, FutureStart);
        ActAs(RoleNames.Owner);

        var dto = await new CancelSessionCommandHandler(Context, CurrentUser).Handle(new CancelSessionCommand(session.Id, null), CancellationToken.None);

        Assert.Null(dto.RescheduledToSessionId);
    }

    [Fact]
    public async Task Cancel_ToAMakeupSessionThatDoesNotExist_IsRejectedAndKeepsTheSessionScheduled()
    {
        var session = AddSession(_course, _room, _teacher, FutureStart);
        ActAs(RoleNames.Owner);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            new CancelSessionCommandHandler(Context, CurrentUser).Handle(new CancelSessionCommand(session.Id, Guid.NewGuid()), CancellationToken.None));

        Assert.Equal(SessionStatus.Scheduled, Context.CourseSessions.Single().Status);
    }

    [Fact]
    public async Task Cancel_FromAnotherBranch_ThrowsForbidden_AndUnknownSessionNotFound()
    {
        var session = AddSession(_course, _room, _teacher, FutureStart);
        ActAs(RoleNames.FrontDesk, _kafrAbdo);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new CancelSessionCommandHandler(Context, CurrentUser).Handle(new CancelSessionCommand(session.Id, null), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CancelSessionCommandHandler(Context, CurrentUser).Handle(new CancelSessionCommand(Guid.NewGuid(), null), CancellationToken.None));
        Assert.Equal(SessionStatus.Scheduled, Context.CourseSessions.Single().Status);
    }

    // ---- Session reads ----

    [Fact]
    public async Task SessionsForCourse_AreOrderedByStart_AndBranchScoped()
    {
        var later = AddSession(_course, _room, _teacher, FutureStart.AddDays(7));
        var earlier = AddSession(_course, _room, _teacher, FutureStart);

        ActAs(RoleNames.FrontDesk, _smouha);
        var result = await new GetSessionsForCourseQueryHandler(Context, CurrentUser).Handle(new GetSessionsForCourseQuery(_course.Id), CancellationToken.None);
        Assert.Equal([earlier.Id, later.Id], result.Select(s => s.Id).ToArray());

        ActAs(RoleNames.FrontDesk, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetSessionsForCourseQueryHandler(Context, CurrentUser).Handle(new GetSessionsForCourseQuery(_course.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetSessionsForCourseQueryHandler(Context, CurrentUser).Handle(new GetSessionsForCourseQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task SessionById_IsVisibleToOwnerBranchStaffAndTheAssignedTeacher_OnlyThem()
    {
        var session = AddSession(_course, _room, _teacher, FutureStart);
        var query = new GetSessionByIdQuery(session.Id);

        ActAs(RoleNames.Owner);
        await new GetSessionByIdQueryHandler(Context, CurrentUser).Handle(query, CancellationToken.None);

        ActAs(RoleNames.FrontDesk, _smouha);
        await new GetSessionByIdQueryHandler(Context, CurrentUser).Handle(query, CancellationToken.None);

        ActAsTeacher(_teacher);
        await new GetSessionByIdQueryHandler(Context, CurrentUser).Handle(query, CancellationToken.None);

        ActAsTeacher(AddTeacher(_smouha));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new GetSessionByIdQueryHandler(Context, CurrentUser).Handle(query, CancellationToken.None));

        ActAs(RoleNames.BranchManager, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new GetSessionByIdQueryHandler(Context, CurrentUser).Handle(query, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetSessionByIdQueryHandler(Context, CurrentUser).Handle(new GetSessionByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    // ---- Attendance ----

    [Fact]
    public async Task MarkAttendance_OnACancelledSession_ThrowsBadRequest()
    {
        // Regression: attendance could previously be recorded for a class that never took place,
        // which also inflated the attendance rate in analytics.
        var student = AddStudent(_smouha);
        Enroll(student, _course);
        var past = DateTime.UtcNow.AddHours(-2);
        var session = AddSession(_course, _room, _teacher, past, status: SessionStatus.Cancelled);
        ActAsTeacher(_teacher);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            new MarkAttendanceCommandHandler(Context, CurrentUser).Handle(new MarkAttendanceCommand(session.Id, student.Id, AttendanceStatus.Present), CancellationToken.None));

        Assert.Contains("cancelled", ex.Errors[0]);
        Assert.Empty(Context.SessionAttendances);
    }

    [Fact]
    public async Task AttendanceForSession_ListsEveryActiveStudent_DefaultingToUnmarked()
    {
        var present = AddStudent(_smouha, "Zaki");
        var unmarked = AddStudent(_smouha, "Amira");
        var dropped = AddStudent(_smouha, "Dropped");
        Enroll(present, _course);
        Enroll(unmarked, _course);
        Enroll(dropped, _course, CourseEnrollmentStatus.Dropped);
        var session = AddSession(_course, _room, _teacher, DateTime.UtcNow.AddHours(-2));
        Context.SessionAttendances.Add(new SessionAttendance { Id = Guid.NewGuid(), CourseSessionId = session.Id, StudentId = present.Id, Status = AttendanceStatus.Present });
        Context.SaveChanges();
        ActAsTeacher(_teacher);

        var result = await new GetAttendanceForSessionQueryHandler(Context, CurrentUser).Handle(new GetAttendanceForSessionQuery(session.Id), CancellationToken.None);

        Assert.Equal(["Amira", "Zaki"], result.Select(r => r.StudentFullName).ToArray());
        Assert.Equal([AttendanceStatus.Unmarked, AttendanceStatus.Present], result.Select(r => r.Status).ToArray());
    }

    [Fact]
    public async Task AttendanceForSession_IsForbiddenToOtherTeachersAndOtherBranches()
    {
        var session = AddSession(_course, _room, _teacher, DateTime.UtcNow.AddHours(-2));
        var query = new GetAttendanceForSessionQuery(session.Id);

        ActAsTeacher(AddTeacher(_smouha));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new GetAttendanceForSessionQueryHandler(Context, CurrentUser).Handle(query, CancellationToken.None));

        ActAs(RoleNames.BranchManager, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new GetAttendanceForSessionQueryHandler(Context, CurrentUser).Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task AttendanceForStudent_StaffSeeAll_ATeacherOnlyTheirOwnCourses_NewestFirst()
    {
        var student = AddStudent(_smouha);
        var otherCourse = AddCourse(_smouha, name: "Scratch");
        var otherTeacher = AddTeacher(_smouha);
        Enroll(student, _course);
        Enroll(student, otherCourse);
        var older = AddSession(_course, _room, _teacher, new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc));
        var newer = AddSession(_course, _room, _teacher, new DateTime(2025, 1, 13, 10, 0, 0, DateTimeKind.Utc));
        var theirs = AddSession(otherCourse, _room, otherTeacher, new DateTime(2025, 1, 8, 10, 0, 0, DateTimeKind.Utc));
        foreach (var s in new[] { older, newer, theirs })
        {
            Context.SessionAttendances.Add(new SessionAttendance { Id = Guid.NewGuid(), CourseSessionId = s.Id, StudentId = student.Id, Status = AttendanceStatus.Present });
        }

        Context.SaveChanges();

        ActAs(RoleNames.FrontDesk, _smouha);
        var staffView = await new GetAttendanceForStudentQueryHandler(Context, CurrentUser).Handle(new GetAttendanceForStudentQuery(student.Id), CancellationToken.None);

        ActAsTeacher(_teacher);
        var teacherView = await new GetAttendanceForStudentQueryHandler(Context, CurrentUser).Handle(new GetAttendanceForStudentQuery(student.Id), CancellationToken.None);

        Assert.Equal([newer.Id, theirs.Id, older.Id], staffView.Select(r => r.CourseSessionId).ToArray());
        Assert.Equal([newer.Id, older.Id], teacherView.Select(r => r.CourseSessionId).ToArray());
    }

    [Fact]
    public async Task AttendanceForStudent_ATeacherWhoDoesNotTeachThemIsForbidden()
    {
        var student = AddStudent(_smouha);
        ActAsTeacher(AddTeacher(_smouha));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetAttendanceForStudentQueryHandler(Context, CurrentUser).Handle(new GetAttendanceForStudentQuery(student.Id), CancellationToken.None));
    }

    // ---- Invoice reads ----

    [Fact]
    public async Task InvoiceById_ReturnsPaidAmountAndBalance_ForBranchStaffOnly()
    {
        var student = AddStudent(_smouha);
        var invoice = AddInvoice(student, 1000, InvoiceStatus.PartiallyPaid);
        AddPayment(invoice, 250);

        ActAs(RoleNames.FrontDesk, _smouha);
        var dto = await new GetInvoiceByIdQueryHandler(Context, CurrentUser).Handle(new GetInvoiceByIdQuery(invoice.Id), CancellationToken.None);
        Assert.Equal((250m, 750m), (dto.AmountPaid, dto.BalanceRemaining));

        ActAs(RoleNames.FrontDesk, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetInvoiceByIdQueryHandler(Context, CurrentUser).Handle(new GetInvoiceByIdQuery(invoice.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetInvoiceByIdQueryHandler(Context, CurrentUser).Handle(new GetInvoiceByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task PaymentsForInvoice_AreListedByDate_ForBranchStaffOnly()
    {
        var student = AddStudent(_smouha);
        var invoice = AddInvoice(student, 1000, InvoiceStatus.PartiallyPaid);
        var later = AddPayment(invoice, 100, new DateOnly(2025, 3, 1));
        var earlier = AddPayment(invoice, 200, new DateOnly(2025, 2, 1));

        ActAs(RoleNames.FrontDesk, _smouha);
        var result = await new GetPaymentsForInvoiceQueryHandler(Context, CurrentUser).Handle(new GetPaymentsForInvoiceQuery(invoice.Id), CancellationToken.None);
        Assert.Equal([earlier.Id, later.Id], result.Select(p => p.Id).ToArray());

        ActAs(RoleNames.FrontDesk, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetPaymentsForInvoiceQueryHandler(Context, CurrentUser).Handle(new GetPaymentsForInvoiceQuery(invoice.Id), CancellationToken.None));
    }
}

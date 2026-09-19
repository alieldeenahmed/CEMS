using CEMS.Application.Analytics.Queries.ExportDashboard;
using CEMS.Application.Analytics.Queries.GetAttendanceTrends;
using CEMS.Application.Analytics.Queries.GetDashboardSummary;
using CEMS.Application.Analytics.Queries.GetEnrollmentFunnel;
using CEMS.Application.Analytics.Queries.GetRevenueSummary;
using CEMS.Application.Analytics.Queries.GetTeacherUtilization;
using CEMS.Application.Common.Exceptions;
using CEMS.Application.Common.Interfaces;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Attendance;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Payments;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Analytics;

/// <summary>
/// The dashboard numbers are pure arithmetic over other tables, which is exactly what breaks silently.
/// Every expected value here is worked out by hand in the comments beside the seed data.
/// </summary>
public class AnalyticsTests : PipelineTestBase
{
    private static readonly DateOnly From = new(2030, 1, 1);
    private static readonly DateOnly To = new(2030, 1, 31);

    private readonly Branch _smouha;
    private readonly Branch _kafrAbdo;

    public AnalyticsTests()
    {
        _smouha = AddBranch("Smouha");
        _kafrAbdo = AddBranch("Kafr Abdo");
        ActAs(RoleNames.Owner);
    }

    // ---- Revenue ----

    private void SeedRevenue()
    {
        var mine = AddStudent(_smouha);
        var theirs = AddStudent(_kafrAbdo);

        var a = AddInvoice(mine, 1000, InvoiceStatus.PartiallyPaid);      // issued Jan 10, in period
        a.IssuedDate = new DateOnly(2030, 1, 10);
        AddPayment(a, 400, new DateOnly(2030, 1, 12));                    // collected in period
        AddPayment(a, 100, new DateOnly(2030, 2, 2));                     // paid after the period

        var b = AddInvoice(mine, 500);                                    // issued Jan 20, nothing paid
        b.IssuedDate = new DateOnly(2030, 1, 20);

        var c = AddInvoice(mine, 900);                                    // issued Feb 1: outside the period
        c.IssuedDate = new DateOnly(2030, 2, 1);

        var d = AddInvoice(mine, 700, InvoiceStatus.Cancelled);           // cancelled: ignored entirely
        d.IssuedDate = new DateOnly(2030, 1, 5);
        AddPayment(d, 50, new DateOnly(2030, 1, 6));

        var e = AddInvoice(theirs, 300, InvoiceStatus.Paid);              // the other branch
        e.IssuedDate = new DateOnly(2030, 1, 15);
        AddPayment(e, 300, new DateOnly(2030, 1, 16));

        Context.SaveChanges();
    }

    [Fact]
    public async Task Revenue_OrgWide_SumsInvoicedCollectedAndOutstanding()
    {
        SeedRevenue();

        var result = await Mediator.Send(new GetRevenueSummaryQuery(null, From, To));

        Assert.Equal(1800m, result.TotalInvoiced);      // 1000 + 500 + 300
        Assert.Equal(700m, result.TotalCollected);      // 400 (Jan 12) + 300 (Jan 16); the Feb payment and the cancelled invoice's 50 don't count
        Assert.Equal(1000m, result.TotalOutstanding);   // (1000-500) + 500 + 0
    }

    [Fact]
    public async Task Revenue_ForOneBranch_ExcludesTheOthers()
    {
        SeedRevenue();

        var result = await Mediator.Send(new GetRevenueSummaryQuery(_smouha.Id, From, To));

        Assert.Equal((1500m, 400m, 1000m), (result.TotalInvoiced, result.TotalCollected, result.TotalOutstanding));
    }

    [Fact]
    public async Task Revenue_PeriodBoundariesAreInclusive()
    {
        var student = AddStudent(_smouha);
        var onFirst = AddInvoice(student, 100);
        onFirst.IssuedDate = From;
        var onLast = AddInvoice(student, 200);
        onLast.IssuedDate = To;
        Context.SaveChanges();

        var result = await Mediator.Send(new GetRevenueSummaryQuery(null, From, To));

        Assert.Equal(300m, result.TotalInvoiced);
    }

    [Fact]
    public async Task Revenue_WithNothingRecorded_IsZeroNotAnError()
    {
        var result = await Mediator.Send(new GetRevenueSummaryQuery(null, From, To));

        Assert.Equal((0m, 0m, 0m), (result.TotalInvoiced, result.TotalCollected, result.TotalOutstanding));
    }

    // ---- Access rule shared by every analytics query ----

    [Fact]
    public async Task Access_OwnerMayOmitTheBranch_AManagerMustNameTheirOwn()
    {
        await Mediator.Send(new GetEnrollmentFunnelQuery(null));

        ActAs(RoleNames.BranchManager, _smouha);
        await Mediator.Send(new GetEnrollmentFunnelQuery(_smouha.Id));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Mediator.Send(new GetEnrollmentFunnelQuery(null)));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Mediator.Send(new GetEnrollmentFunnelQuery(_kafrAbdo.Id)));
    }

    [Fact]
    public async Task Access_EveryAnalyticsQueryEnforcesIt()
    {
        ActAs(RoleNames.BranchManager, _smouha);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Mediator.Send(new GetRevenueSummaryQuery(null, From, To)));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Mediator.Send(new GetAttendanceTrendsQuery(_kafrAbdo.Id, From, To)));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Mediator.Send(new GetTeacherUtilizationQuery(null, From, To)));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Mediator.Send(new GetDashboardSummaryQuery(_kafrAbdo.Id, From, To)));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Mediator.Send(new ExportDashboardPdfQuery(null, From, To)));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Mediator.Send(new ExportDashboardExcelQuery(_kafrAbdo.Id, From, To)));
        Assert.Null(Reports.Dashboard);
    }

    // ---- Attendance ----

    [Fact]
    public async Task AttendanceTrends_CountsEachStatus_AndTheRateIsOnlyThePresentShare()
    {
        var course = AddCourse(_smouha);
        var room = AddRoom(_smouha);
        var teacher = AddTeacher(_smouha);
        var session = AddSession(course, room, teacher, new DateTime(2030, 1, 10, 10, 0, 0, DateTimeKind.Utc));
        var outside = AddSession(course, room, teacher, new DateTime(2030, 3, 10, 10, 0, 0, DateTimeKind.Utc));

        void Mark(Domain.Courses.CourseSession s, AttendanceStatus status)
        {
            Context.SessionAttendances.Add(new SessionAttendance { Id = Guid.NewGuid(), CourseSessionId = s.Id, StudentId = AddStudent(_smouha).Id, Status = status });
        }

        Mark(session, AttendanceStatus.Present);
        Mark(session, AttendanceStatus.Present);
        Mark(session, AttendanceStatus.Present);
        Mark(session, AttendanceStatus.Late);
        Mark(session, AttendanceStatus.Absent);
        Mark(session, AttendanceStatus.Excused);
        Mark(outside, AttendanceStatus.Absent);
        Context.SaveChanges();

        var result = await Mediator.Send(new GetAttendanceTrendsQuery(null, From, To));

        Assert.Equal((6, 3, 1, 1, 1), (result.TotalRecords, result.PresentCount, result.AbsentCount, result.LateCount, result.ExcusedCount));
        Assert.Equal(0.5, result.AttendanceRate);   // 3 present of 6; a late student does not count as present
    }

    [Fact]
    public async Task AttendanceTrends_WithNoRecords_HasAZeroRateInsteadOfDividingByZero()
    {
        var result = await Mediator.Send(new GetAttendanceTrendsQuery(null, From, To));

        Assert.Equal((0, 0.0), (result.TotalRecords, result.AttendanceRate));
    }

    [Fact]
    public async Task AttendanceTrends_ForOneBranch_IgnoresOtherBranchesSessions()
    {
        var mineSession = AddSession(AddCourse(_smouha), AddRoom(_smouha), AddTeacher(_smouha), new DateTime(2030, 1, 10, 10, 0, 0, DateTimeKind.Utc));
        var theirSession = AddSession(AddCourse(_kafrAbdo), AddRoom(_kafrAbdo), AddTeacher(_kafrAbdo), new DateTime(2030, 1, 10, 10, 0, 0, DateTimeKind.Utc));
        Context.SessionAttendances.AddRange(
            new SessionAttendance { Id = Guid.NewGuid(), CourseSessionId = mineSession.Id, StudentId = AddStudent(_smouha).Id, Status = AttendanceStatus.Present },
            new SessionAttendance { Id = Guid.NewGuid(), CourseSessionId = theirSession.Id, StudentId = AddStudent(_kafrAbdo).Id, Status = AttendanceStatus.Absent });
        Context.SaveChanges();

        var result = await Mediator.Send(new GetAttendanceTrendsQuery(_smouha.Id, From, To));

        Assert.Equal((1, 1, 0), (result.TotalRecords, result.PresentCount, result.AbsentCount));
    }

    // ---- Enrollment funnel ----

    [Fact]
    public async Task EnrollmentFunnel_SeparatesEverEnrolledFromCurrentlyActive()
    {
        var course = AddCourse(_smouha);
        Enroll(AddStudent(_smouha, "Active"), course);
        Enroll(AddStudent(_smouha, "Dropped"), course, CourseEnrollmentStatus.Dropped);
        Enroll(AddStudent(_smouha, "Waitlisted"), course, CourseEnrollmentStatus.Waitlisted);
        AddStudent(_smouha, "Never enrolled A");
        AddStudent(_smouha, "Never enrolled B");
        Enroll(AddStudent(_kafrAbdo, "Elsewhere"), AddCourse(_kafrAbdo));

        var branch = await Mediator.Send(new GetEnrollmentFunnelQuery(_smouha.Id));
        var orgWide = await Mediator.Send(new GetEnrollmentFunnelQuery(null));

        Assert.Equal((5, 3, 1), (branch.TotalStudents, branch.StudentsWithAnyEnrollment, branch.StudentsWithActiveEnrollment));
        Assert.Equal((6, 4, 2), (orgWide.TotalStudents, orgWide.StudentsWithAnyEnrollment, orgWide.StudentsWithActiveEnrollment));
    }

    [Fact]
    public async Task EnrollmentFunnel_ACountsAStudentOnceEvenWithSeveralEnrollments()
    {
        var student = AddStudent(_smouha);
        Enroll(student, AddCourse(_smouha, name: "Python"));
        Enroll(student, AddCourse(_smouha, name: "Scratch"));

        var result = await Mediator.Send(new GetEnrollmentFunnelQuery(null));

        Assert.Equal((1, 1, 1), (result.TotalStudents, result.StudentsWithAnyEnrollment, result.StudentsWithActiveEnrollment));
    }

    // ---- Teacher utilization ----

    private Teacher AddNamedTeacher(string name, Branch branch)
    {
        var teacher = AddTeacher(branch);
        Identity.AddUser(new AuthenticatedUser(teacher.UserId, $"{name.ToLower()}@codecamp.demo", name, [RoleNames.Teacher], Array.Empty<Guid>(), true));
        return teacher;
    }

    private void AddAvailability(Teacher teacher, Branch branch, DayOfWeek day, int startHour, int endHour)
    {
        Context.TeacherAvailabilities.Add(new TeacherAvailability
        {
            Id = Guid.NewGuid(), TeacherId = teacher.Id, BranchId = branch.Id, DayOfWeek = day,
            StartTime = new TimeOnly(startHour, 0), EndTime = new TimeOnly(endHour, 0)
        });
        Context.SaveChanges();
    }

    [Fact]
    public async Task Utilization_IsScheduledHoursOverWeeklyAvailabilityTimesWeeks()
    {
        var ahmed = AddNamedTeacher("Ahmed", _smouha);
        var course = AddCourse(_smouha);
        var room = AddRoom(_smouha);
        AddAvailability(ahmed, _smouha, DayOfWeek.Monday, 9, 17);      // 8h
        AddAvailability(ahmed, _smouha, DayOfWeek.Wednesday, 9, 13);   // +4h = 12h per week
        AddSession(course, room, ahmed, new DateTime(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc), TimeSpan.FromHours(1));
        AddSession(course, room, ahmed, new DateTime(2030, 1, 9, 10, 0, 0, DateTimeKind.Utc), TimeSpan.FromHours(2));
        AddSession(course, room, ahmed, new DateTime(2030, 1, 8, 10, 0, 0, DateTimeKind.Utc), TimeSpan.FromHours(5), SessionStatus.Cancelled);

        var result = await Mediator.Send(new GetTeacherUtilizationQuery(null, new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 14)));

        var row = Assert.Single(result);
        Assert.Equal("Ahmed", row.TeacherFullName);
        Assert.Equal(3.0, row.ScheduledHours);       // 1h + 2h; the cancelled 5h session is excluded
        Assert.Equal(24.0, row.AvailableHours);      // 12h/week over exactly 2 weeks
        Assert.Equal(0.125, row.UtilizationRate);
    }

    [Fact]
    public async Task Utilization_ATeacherWithNoAvailability_HasAZeroRateEvenIfScheduled()
    {
        var teacher = AddNamedTeacher("Sara", _smouha);
        AddSession(AddCourse(_smouha), AddRoom(_smouha), teacher, new DateTime(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc));

        var row = Assert.Single(await Mediator.Send(new GetTeacherUtilizationQuery(null, From, To)));

        Assert.Equal((1.0, 0.0, 0.0), (row.ScheduledHours, row.AvailableHours, row.UtilizationRate));
    }

    [Fact]
    public async Task Utilization_ForOneBranch_OnlyCountsThatBranchsTeachersSessionsAndAvailability()
    {
        var floating = AddNamedTeacher("Floaty", _smouha);
        Context.TeacherBranches.Add(new TeacherBranch { TeacherId = floating.Id, BranchId = _kafrAbdo.Id });
        Context.SaveChanges();
        AddAvailability(floating, _smouha, DayOfWeek.Monday, 9, 17);      // 8h at Smouha
        AddAvailability(floating, _kafrAbdo, DayOfWeek.Tuesday, 9, 13);   // 4h at Kafr Abdo
        AddSession(AddCourse(_smouha), AddRoom(_smouha), floating, new DateTime(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc));
        AddSession(AddCourse(_kafrAbdo), AddRoom(_kafrAbdo), floating, new DateTime(2030, 1, 8, 10, 0, 0, DateTimeKind.Utc), TimeSpan.FromHours(3));
        AddNamedTeacher("Other", _kafrAbdo);

        var smouhaOnly = Assert.Single(await Mediator.Send(new GetTeacherUtilizationQuery(_smouha.Id, new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 7))));

        Assert.Equal(1.0, smouhaOnly.ScheduledHours);   // the 3h Kafr Abdo session is not counted here
        Assert.Equal(8.0, smouhaOnly.AvailableHours);   // 8h/week x 1 week; the Kafr Abdo window is not counted
    }

    // ---- Dashboard summary and exports ----

    [Fact]
    public async Task DashboardSummary_ComposesEveryMetricForTheSameScopeAndPeriod()
    {
        SeedRevenue();
        Enroll(AddStudent(_smouha), AddCourse(_smouha));

        var summary = await Mediator.Send(new GetDashboardSummaryQuery(_smouha.Id, From, To));

        Assert.Equal((_smouha.Id, From, To), (summary.BranchId, summary.PeriodStart, summary.PeriodEnd));
        Assert.Equal(1500m, summary.Revenue.TotalInvoiced);
        Assert.Equal(1000m, summary.Revenue.TotalOutstanding);
        Assert.Equal(0, summary.AttendanceTrends.TotalRecords);
        Assert.True(summary.EnrollmentFunnel.TotalStudents >= 1);
        Assert.NotNull(summary.TeacherUtilization);
    }

    [Fact]
    public async Task Exports_RenderTheSameSummaryAsPdfOrExcel()
    {
        SeedRevenue();

        var pdf = await Mediator.Send(new ExportDashboardPdfQuery(null, From, To));
        Assert.Equal("pdf", Reports.DashboardFormat);
        Assert.Equal([0x25, 0x50], pdf);
        Assert.Equal(1800m, Reports.Dashboard!.Revenue.TotalInvoiced);

        var excel = await Mediator.Send(new ExportDashboardExcelQuery(null, From, To));
        Assert.Equal("excel", Reports.DashboardFormat);
        Assert.Equal([0x50, 0x4B], excel);
    }
}

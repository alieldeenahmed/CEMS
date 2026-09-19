using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace CEMS.Api.Tests;

/// <summary>
/// Whole business workflows driven over HTTP as the people who really do them, each on a fresh copy of
/// the two-branch center. These exercise the controllers, the pipeline, the real database and the real
/// PDF/Excel generators together: the closest thing here to a day at the center.
/// </summary>
public class WorkflowTests
{
    private static async Task<ApiWorld> NewWorld()
    {
        var world = new ApiWorld();
        await world.InitializeAsync();
        return world;
    }

    /// <summary>Asserts success and returns the JSON body, or an empty object for a 204 No Content.</summary>
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {text}");
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text).RootElement.Clone();
    }

    private static async Task<JsonElement> Get(HttpClient client, string url) => await Json(await client.GetAsync(url));

    private static async Task<JsonElement> Post(HttpClient client, string url, object? body = null) =>
        await Json(await client.PostAsJsonAsync(url, body ?? new { }));

    private static async Task<byte[]> Pdf(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        return bytes;
    }

    private static string Status(JsonElement e) => e.GetProperty("status").GetString()!;

    // ---- Billing: package → invoice → instalments → paid ----

    [Fact]
    public async Task Billing_AnInvoiceMovesFromPendingToPartiallyPaidToPaid_AsInstalmentsArrive()
    {
        using var w = await NewWorld();
        var package = await w.CreateAsync($"/api/courses/{w.Course}/packages", new { sessionCount = 12, price = 2400 });
        var invoice = await w.CreateAsync($"/api/students/{w.TaughtStudent}/invoices", new { packageId = package, dueDate = "2030-02-01" }, w.FrontDeskSmouha);

        Assert.Equal("Pending", Status(await Get(w.FrontDeskSmouha, $"/api/invoices/{invoice}")));

        // 600 of 2400 is a quarter: the invoice must stay partially paid (regression: any payment over 50% used to mark it paid).
        await Post(w.FrontDeskSmouha, $"/api/invoices/{invoice}/payments", new { amountPaid = 600, paymentDate = "2030-01-10", method = "Cash" });
        var partial = await Get(w.FrontDeskSmouha, $"/api/invoices/{invoice}");
        Assert.Equal("PartiallyPaid", Status(partial));
        Assert.Equal(1800m, partial.GetProperty("balanceRemaining").GetDecimal());
        Assert.Equal(1800m, (await Get(w.FrontDeskSmouha, $"/api/students/{w.TaughtStudent}/balance")).GetProperty("totalOutstanding").GetDecimal());

        await Post(w.FrontDeskSmouha, $"/api/invoices/{invoice}/payments", new { amountPaid = 1800, paymentDate = "2030-01-20", method = "Card" });
        Assert.Equal("Paid", Status(await Get(w.FrontDeskSmouha, $"/api/invoices/{invoice}")));
        Assert.Equal(2, (await Get(w.FrontDeskSmouha, $"/api/invoices/{invoice}/payments")).GetArrayLength());
        Assert.Single((await Get(w.FrontDeskSmouha, $"/api/students/{w.TaughtStudent}/invoices")).EnumerateArray());
    }

    [Fact]
    public async Task Billing_CancellationRules_AndAPackageRepriceNeverChangesAnIssuedInvoice()
    {
        using var w = await NewWorld();
        var package = await w.CreateAsync($"/api/courses/{w.Course}/packages", new { sessionCount = 12, price = 2400 });
        var paidInvoice = await w.CreateAsync($"/api/students/{w.TaughtStudent}/invoices", new { packageId = package, dueDate = "2030-02-01" });
        var openInvoice = await w.CreateAsync($"/api/students/{w.TaughtStudent}/invoices", new { amount = 150, dueDate = "2030-02-01" });
        await Post(w.Owner, $"/api/invoices/{paidInvoice}/payments", new { amountPaid = 2400, paymentDate = "2030-01-10", method = "Transfer" });

        Assert.Equal(HttpStatusCode.Forbidden, (await w.FrontDeskSmouha.PostAsync($"/api/invoices/{openInvoice}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await w.ManagerSmouha.PostAsync($"/api/invoices/{paidInvoice}/cancel", null)).StatusCode);
        Assert.Equal("Cancelled", Status(await Json(await w.ManagerSmouha.PostAsync($"/api/invoices/{openInvoice}/cancel", null))));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await w.Owner.PostAsJsonAsync($"/api/invoices/{openInvoice}/payments", new { amountPaid = 10, paymentDate = "2030-01-11", method = "Cash" })).StatusCode);

        var repriced = await Json(await w.ManagerSmouha.PutAsJsonAsync($"/api/packages/{package}", new { sessionCount = 12, price = 3000 }));
        Assert.Equal(3000m, repriced.GetProperty("price").GetDecimal());
        Assert.Equal(2400m, (await Get(w.Owner, $"/api/invoices/{paidInvoice}")).GetProperty("amount").GetDecimal());

        Assert.Equal(HttpStatusCode.BadRequest, (await w.Owner.DeleteAsync($"/api/packages/{package}")).StatusCode);   // an invoice was issued against it
        Assert.Single((await Get(w.FrontDeskSmouha, $"/api/courses/{w.Course}/packages")).EnumerateArray());
        Assert.Equal(3000m, (await Get(w.FrontDeskSmouha, $"/api/packages/{package}")).GetProperty("price").GetDecimal());
    }

    // ---- Academics: attendance → exam → grade → report card ----

    [Fact]
    public async Task Academics_ATeacherTakesAttendance_SetsAnExam_GradesIt_AndTheFrontDeskPrintsTheReportCard()
    {
        using var w = await NewWorld();
        var now = DateTime.UtcNow;
        var past = await w.CreateAsync($"/api/courses/{w.Course}/sessions",
            ApiWorld.SessionBody(w.SmouhaRoom, w.TeacherId, now.AddHours(-2), @override: true, reason: "Catch-up class"), w.ManagerSmouha);

        // Only the assigned teacher (or a manager) takes attendance; the front desk cannot.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await w.FrontDeskSmouha.PostAsJsonAsync($"/api/sessions/{past}/attendance", new { studentId = w.TaughtStudent, status = "Present" })).StatusCode);
        await Post(w.Teacher, $"/api/sessions/{past}/attendance", new { studentId = w.TaughtStudent, status = "Present" });

        var roster = await Get(w.Teacher, $"/api/sessions/{past}/attendance");
        Assert.Equal("Present", roster.EnumerateArray().Single().GetProperty("status").GetString());
        Assert.Single((await Get(w.Teacher, $"/api/students/{w.TaughtStudent}/attendance")).EnumerateArray());

        var exam = await w.CreateAsync($"/api/courses/{w.Course}/exams", new { name = "Python Midterm", maxScore = 100, examDate = "2030-02-01" }, w.Teacher);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await w.Teacher.PostAsJsonAsync($"/api/exams/{exam}/grades", new { studentId = w.TaughtStudent, score = 101, comments = "too high" })).StatusCode);
        var grade = await Post(w.Teacher, $"/api/exams/{exam}/grades", new { studentId = w.TaughtStudent, score = 88, comments = "Great grasp of loops" });
        Assert.Equal(88m, grade.GetProperty("score").GetDecimal());

        Assert.Equal(88m, (await Get(w.ManagerSmouha, $"/api/exams/{exam}/grades")).EnumerateArray().Single().GetProperty("score").GetDecimal());
        Assert.Single((await Get(w.Teacher, $"/api/students/{w.TaughtStudent}/grades")).EnumerateArray());
        Assert.Single((await Get(w.FrontDeskSmouha, $"/api/courses/{w.Course}/exams")).EnumerateArray());
        Assert.Equal("Python Midterm", (await Get(w.FrontDeskSmouha, $"/api/exams/{exam}")).GetProperty("name").GetString());

        await Pdf(w.FrontDeskSmouha, $"/api/students/{w.TaughtStudent}/report-card");

        var renamed = await Json(await w.Teacher.PutAsJsonAsync($"/api/exams/{exam}", new { name = "Python Final", maxScore = 50, examDate = "2030-03-01" }));
        Assert.Equal("Python Final", renamed.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await w.Teacher.DeleteAsync($"/api/exams/{exam}")).StatusCode);
    }

    // ---- Enrollment and capacity ----

    [Fact]
    public async Task Enrollment_ACourseThatIsFullWaitlistsTheNextStudent_UntilAStaffMemberPromotesThem()
    {
        using var w = await NewWorld();
        var tinyRoom = await w.CreateAsync($"/api/branches/{w.Smouha}/rooms", new { name = "Booth", capacity = 1 });
        var curriculum = await w.CreateAsync("/api/curricula", new { name = "1:1", description = "one on one" });
        var course = await w.CreateAsync("/api/courses", new { name = "JavaScript 1:1", deliveryMode = "OneOnOne", curriculumId = curriculum, branchId = w.Smouha });
        await w.CreateAsync($"/api/courses/{course}/sessions", ApiWorld.SessionBody(tinyRoom, w.TeacherId, ApiWorld.SessionStart.AddDays(7)));

        var first = await Post(w.FrontDeskSmouha, $"/api/courses/{course}/enrollments", new { studentId = w.TaughtStudent });
        var second = await Post(w.FrontDeskSmouha, $"/api/courses/{course}/enrollments", new { studentId = w.UntaughtStudent });

        Assert.Equal("Active", Status(first));
        Assert.Equal("Waitlisted", Status(second));
        Assert.Equal(1, second.GetProperty("position").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await w.FrontDeskSmouha.PostAsJsonAsync($"/api/courses/{course}/enrollments", new { studentId = w.UntaughtStudent })).StatusCode);   // already waitlisted

        var promoted = await Post(w.FrontDeskSmouha, $"/api/courses/enrollments/{second.GetProperty("id").GetGuid()}/promote");
        Assert.Equal("Active", Status(promoted));

        Assert.Equal(HttpStatusCode.NoContent, (await w.FrontDeskSmouha.DeleteAsync($"/api/courses/enrollments/{first.GetProperty("id").GetGuid()}")).StatusCode);
        var enrollments = await Get(w.ManagerSmouha, $"/api/courses/{course}/enrollments");
        Assert.Equal(["Active", "Dropped"], enrollments.EnumerateArray().Select(Status).OrderBy(s => s).ToArray());
        Assert.Equal(2, (await Get(w.FrontDeskSmouha, $"/api/students/{w.TaughtStudent}/enrollments")).GetArrayLength());
    }

    // ---- Catalog maintenance ----

    [Fact]
    public async Task Catalog_BranchesRoomsCurriculaAndCoursesCanBeEditedAndRemoved()
    {
        using var w = await NewWorld();

        var branch = await Json(await w.Owner.PutAsJsonAsync($"/api/branches/{w.Smouha}", new { name = "CodeCamp Smouha HQ", address = "New address", phone = "0345670000", isActive = true }));
        Assert.Equal("CodeCamp Smouha HQ", branch.GetProperty("name").GetString());
        Assert.Equal("CodeCamp Smouha HQ", (await Get(w.ManagerSmouha, $"/api/branches/{w.Smouha}")).GetProperty("name").GetString());

        var room = await w.CreateAsync($"/api/branches/{w.Smouha}/rooms", new { name = "Temp", capacity = 4 }, w.ManagerSmouha);
        Assert.Equal(20, (await Json(await w.ManagerSmouha.PutAsJsonAsync($"/api/rooms/{room}", new { name = "Temp B", capacity = 20 }))).GetProperty("capacity").GetInt32());
        Assert.Equal("Temp B", (await Get(w.FrontDeskSmouha, $"/api/rooms/{room}")).GetProperty("name").GetString());
        Assert.Equal(2, (await Get(w.FrontDeskSmouha, $"/api/branches/{w.Smouha}/rooms")).GetArrayLength());
        Assert.Equal(HttpStatusCode.NoContent, (await w.ManagerSmouha.DeleteAsync($"/api/rooms/{room}")).StatusCode);

        var curriculum = await w.CreateAsync("/api/curricula", new { name = "Temporary", description = "to be removed" }, w.ManagerSmouha);
        await Json(await w.ManagerSmouha.PutAsJsonAsync($"/api/curricula/{curriculum}", new { name = "Temporary v2", description = "edited" }));
        Assert.Equal("Temporary v2", (await Get(w.Teacher, $"/api/curricula/{curriculum}")).GetProperty("name").GetString());
        var course = await w.CreateAsync("/api/courses", new { name = "Throwaway", deliveryMode = "Group", curriculumId = curriculum, branchId = w.Smouha }, w.ManagerSmouha);
        Assert.Equal(HttpStatusCode.BadRequest, (await w.ManagerSmouha.DeleteAsync($"/api/curricula/{curriculum}")).StatusCode);   // it still has a course

        var renamed = await Json(await w.ManagerSmouha.PutAsJsonAsync($"/api/courses/{course}", new { name = "Throwaway 2", deliveryMode = "OneOnOne" }));
        Assert.Equal("OneOnOne", renamed.GetProperty("deliveryMode").GetString());
        Assert.Equal("Throwaway 2", (await Get(w.FrontDeskSmouha, $"/api/courses/{course}")).GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await w.ManagerSmouha.DeleteAsync($"/api/courses/{course}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await w.ManagerSmouha.DeleteAsync($"/api/curricula/{curriculum}")).StatusCode);
    }

    // ---- Teachers: qualifications, availability, substitution, passwords ----

    [Fact]
    public async Task Teachers_QualificationsAvailabilityAndSubstitution()
    {
        using var w = await NewWorld();

        await Post(w.ManagerSmouha, $"/api/teachers/{w.TeacherId}/qualifications", new { courseId = w.Course });
        var qualified = await Get(w.Teacher, $"/api/teachers/{w.TeacherId}/qualifications");
        Assert.Equal("Python Fundamentals", qualified.EnumerateArray().Single().GetProperty("courseName").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await w.ManagerSmouha.DeleteAsync($"/api/teachers/{w.TeacherId}/qualifications/{w.Course}")).StatusCode);
        Assert.Equal(0, (await Get(w.ManagerSmouha, $"/api/teachers/{w.TeacherId}/qualifications")).GetArrayLength());

        var window = await Post(w.Teacher, $"/api/teachers/{w.TeacherId}/availability", new { branchId = w.Smouha, dayOfWeek = "Wednesday", startTime = "10:00", endTime = "12:00" });
        Assert.Equal(2, (await Get(w.Teacher, $"/api/teachers/{w.TeacherId}/availability")).GetArrayLength());
        Assert.Equal(HttpStatusCode.NoContent, (await w.Teacher.DeleteAsync($"/api/teachers/availability/{window.GetProperty("id").GetGuid()}")).StatusCode);

        // A second teacher, then substitute them into the world's Monday session.
        var userId = (await Post(w.Owner, "/api/users/staff", new { email = "sara@codecamp.demo", password = "DemoPass123", fullName = "Sara Ibrahim", phoneNumber = "0100", role = "Teacher", branchId = (Guid?)null })).GetProperty("userId").GetGuid();
        var sara = await w.CreateAsync("/api/teachers", new { userId, hireDate = "2024-06-01", payType = "PerSession", payRate = 100 });
        (await w.Owner.PostAsJsonAsync($"/api/teachers/{sara}/branches", new { branchId = w.Smouha })).EnsureSuccessStatusCode();
        await Post(w.Owner, $"/api/teachers/{sara}/availability", new { branchId = w.Smouha, dayOfWeek = "Monday", startTime = "09:00", endTime = "17:00" });

        var substituted = await Post(w.ManagerSmouha, $"/api/sessions/{w.Session}/substitute-teacher", new { newTeacherId = sara, @override = false, overrideReason = (string?)null });
        Assert.Equal(sara, substituted.GetProperty("teacherId").GetGuid());
        Assert.Equal(sara, (await Get(w.FrontDeskSmouha, $"/api/sessions/{w.Session}")).GetProperty("teacherId").GetGuid());

        // Cancel it, linking a makeup class.
        var makeup = await w.CreateAsync($"/api/courses/{w.Course}/sessions", ApiWorld.SessionBody(w.SmouhaRoom, sara, ApiWorld.SessionStart.AddDays(7)));
        var cancelled = await Post(w.FrontDeskSmouha, $"/api/sessions/{w.Session}/cancel", new { rescheduledToSessionId = makeup });
        Assert.Equal("Cancelled", Status(cancelled));
        Assert.Equal(makeup, cancelled.GetProperty("rescheduledToSessionId").GetGuid());
        Assert.Equal(2, (await Get(w.FrontDeskSmouha, $"/api/courses/{w.Course}/sessions")).GetArrayLength());

        // Read-side endpoints a teacher and the desk use daily.
        Assert.Equal("Ahmed Nabil", (await Get(w.Teacher, "/api/teachers/my-profile")).GetProperty("fullName").GetString());
        Assert.Equal(0, (await Get(w.Teacher, "/api/teachers/my-schedule")).GetArrayLength());   // the only session was handed to Sara
        Assert.Equal(0, (await Get(w.Teacher, "/api/courses/my-courses")).GetArrayLength());   // "my courses" follows scheduled sessions
        Assert.Equal(2, (await Get(w.FrontDeskSmouha, "/api/teachers")).GetArrayLength());
        Assert.Equal("Ahmed Nabil", (await Get(w.FrontDeskSmouha, $"/api/teachers/{w.TeacherId}")).GetProperty("fullName").GetString());
        Assert.Equal(0, (await Get(w.ManagerSmouha, "/api/teachers/candidates")).GetArrayLength());

        var updated = await Json(await w.Owner.PutAsJsonAsync($"/api/teachers/{w.TeacherId}", new { hireDate = "2023-01-01", payType = "Fixed", payRate = 9000 }));
        Assert.Equal("Fixed", updated.GetProperty("payType").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await w.Owner.DeleteAsync($"/api/teachers/{w.TeacherId}/branches/{w.Smouha}")).StatusCode);
    }

    [Fact]
    public async Task Passwords_ABranchManagerResetsATeachersPassword_AndOnlyTheNewOneWorks()
    {
        using var w = await NewWorld();

        var reset = await w.ManagerSmouha.PostAsJsonAsync($"/api/users/staff/{w.TeacherUserId}/reset-password", new { newPassword = "BrandNewPass1" });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        var http = w.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsJsonAsync("/api/auth/login", new { email = "teacher.smouha@codecamp.demo", password = "DemoPass123" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsJsonAsync("/api/auth/login", new { email = "teacher.smouha@codecamp.demo", password = "BrandNewPass1" })).StatusCode);

        // A manager cannot reset another branch's staff or a peer manager.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await w.ManagerKafrAbdo.PostAsJsonAsync($"/api/users/staff/{w.TeacherUserId}/reset-password", new { newPassword = "AnotherPass12" })).StatusCode);
    }

    [Fact]
    public async Task Staff_TheBranchManagersRosterListsOnlyTheirTeachersAndFrontDesk()
    {
        using var w = await NewWorld();

        var roster = await Get(w.ManagerSmouha, "/api/users/staff/my-branch");
        var names = roster.EnumerateArray().Select(u => u.GetProperty("fullName").GetString()).OrderBy(n => n).ToArray();

        Assert.Equal(["Ahmed Nabil", "Mariam Younis"], names);
        Assert.Equal(6, (await Get(w.Owner, "/api/users/staff")).GetArrayLength());
    }

    // ---- Payroll ----

    [Fact]
    public async Task Payroll_ATeachersRunIsGeneratedApprovedAndPaid_AndOnlyThePayeeAndOwnerCanSeeIt()
    {
        using var w = await NewWorld();

        var run = await Post(w.Owner, $"/api/teachers/{w.TeacherId}/payroll-runs", new { periodStart = "2030-01-01", periodEnd = "2030-01-31" });
        var runId = run.GetProperty("id").GetGuid();
        Assert.Equal((150m, "Draft"), (run.GetProperty("totalAmount").GetDecimal(), Status(run)));   // one 1-hour session x 150/hour
        Assert.Equal(HttpStatusCode.BadRequest,
            (await w.Owner.PostAsJsonAsync($"/api/teachers/{w.TeacherId}/payroll-runs", new { periodStart = "2030-01-15", periodEnd = "2030-02-15" })).StatusCode);   // overlaps

        Assert.Equal(HttpStatusCode.Forbidden, (await w.ManagerSmouha.GetAsync($"/api/payroll-runs/{runId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await w.Owner.PostAsync($"/api/payroll-runs/{runId}/mark-paid", null)).StatusCode);   // must be approved first

        Assert.Equal("Approved", Status(await Json(await w.Owner.PostAsync($"/api/payroll-runs/{runId}/approve", null))));

        Assert.Equal(runId, (await Get(w.Teacher, "/api/teachers/my-payroll-runs")).EnumerateArray().Single().GetProperty("id").GetGuid());
        Assert.Equal(150m, (await Get(w.Teacher, $"/api/payroll-runs/{runId}/line-items")).EnumerateArray().Single().GetProperty("amount").GetDecimal());
        await Pdf(w.Teacher, $"/api/payroll-runs/{runId}/paystub");
        Assert.Equal(HttpStatusCode.Forbidden, (await w.FrontDeskSmouha.GetAsync($"/api/payroll-runs/{runId}/paystub")).StatusCode);

        Assert.Equal("Paid", Status(await Json(await w.Owner.PostAsync($"/api/payroll-runs/{runId}/mark-paid", null))));
        Assert.Equal(1, (await Get(w.Owner, $"/api/teachers/{w.TeacherId}/payroll-runs")).GetArrayLength());
    }

    [Fact]
    public async Task Payroll_AFrontDeskMembersManualRun_CanBeEditedWhileDraftOnly()
    {
        using var w = await NewWorld();
        var frontDesk = (await Get(w.Owner, "/api/users/staff")).EnumerateArray().Single(u => u.GetProperty("email").GetString() == "fd.smouha@codecamp.demo").GetProperty("userId").GetGuid();

        var run = await Post(w.Owner, $"/api/users/staff/{frontDesk}/payroll-runs", new { periodStart = "2030-01-01", periodEnd = "2030-01-31", amount = 3200 });
        var runId = run.GetProperty("id").GetGuid();

        Assert.Equal(3300m, (await Json(await w.Owner.PutAsJsonAsync($"/api/staff-payroll-runs/{runId}", new { amount = 3300 }))).GetProperty("amount").GetDecimal());
        await Post(w.Owner, $"/api/staff-payroll-runs/{runId}/approve");
        Assert.Equal(HttpStatusCode.BadRequest, (await w.Owner.PutAsJsonAsync($"/api/staff-payroll-runs/{runId}", new { amount = 1 })).StatusCode);   // no edits after approval

        Assert.Equal(runId, (await Get(w.FrontDeskSmouha, "/api/users/my-staff-payroll-runs")).EnumerateArray().Single().GetProperty("id").GetGuid());
        await Pdf(w.FrontDeskSmouha, $"/api/staff-payroll-runs/{runId}/paystub");
        Assert.Equal(HttpStatusCode.Forbidden, (await w.ManagerSmouha.GetAsync($"/api/staff-payroll-runs/{runId}/paystub")).StatusCode);   // someone else's stub

        Assert.Equal("Paid", Status(await Json(await w.Owner.PostAsync($"/api/staff-payroll-runs/{runId}/mark-paid", null))));
        Assert.Equal(1, (await Get(w.Owner, $"/api/users/staff/{frontDesk}/payroll-runs")).GetArrayLength());
    }

    // ---- Guardians ----

    [Fact]
    public async Task Guardians_CanBeEditedAndUnlinkedByTheirBranch()
    {
        using var w = await NewWorld();

        var edited = await Json(await w.FrontDeskSmouha.PutAsJsonAsync($"/api/guardians/{w.SmouhaGuardian}", new { fullName = "Hany M. Mahmoud", phone = "01199999999", email = "hany.new@example.com" }));
        Assert.Equal("Hany M. Mahmoud", edited.GetProperty("fullName").GetString());
        Assert.Equal("hany.new@example.com", (await Get(w.ManagerSmouha, $"/api/guardians/{w.SmouhaGuardian}")).GetProperty("email").GetString());
        Assert.Equal(1, (await Get(w.FrontDeskSmouha, $"/api/students/{w.TaughtStudent}/guardians")).GetArrayLength());

        var extra = await w.CreateAsync("/api/guardians", new { fullName = "Second Parent", phone = "0100", email = "second@example.com" }, w.FrontDeskSmouha);
        await w.FrontDeskSmouha.PostAsJsonAsync($"/api/students/{w.TaughtStudent}/guardians", new { guardianId = extra, relationshipType = "Mother", isPrimaryContact = false });
        Assert.Equal(2, (await Get(w.FrontDeskSmouha, $"/api/students/{w.TaughtStudent}/guardians")).GetArrayLength());
        Assert.Equal(HttpStatusCode.NoContent, (await w.FrontDeskSmouha.DeleteAsync($"/api/students/{w.TaughtStudent}/guardians/{extra}")).StatusCode);
        Assert.Equal(1, (await Get(w.FrontDeskSmouha, $"/api/students/{w.TaughtStudent}/guardians")).GetArrayLength());

        var student = await Json(await w.FrontDeskSmouha.PutAsJsonAsync($"/api/students/{w.TaughtStudent}", new { fullName = "Khaled H.", dateOfBirth = "2013-04-12", gender = "Male", status = "Paused" }));
        Assert.Equal("Paused", Status(student));
    }

    // ---- Analytics and exports ----

    [Fact]
    public async Task Analytics_EveryDashboardMetricIsServed_AndTheExportsAreRealFiles()
    {
        using var w = await NewWorld();
        const string q = "periodStart=2030-01-01&periodEnd=2030-01-31";

        Assert.Equal(1, (await Get(w.Owner, "/api/analytics/enrollment-funnel")).GetProperty("studentsWithActiveEnrollment").GetInt32());
        Assert.Equal(0m, (await Get(w.Owner, $"/api/analytics/revenue?{q}")).GetProperty("totalInvoiced").GetDecimal());
        Assert.Equal(0, (await Get(w.Owner, $"/api/analytics/attendance-trends?{q}")).GetProperty("totalRecords").GetInt32());
        Assert.Equal("Ahmed Nabil", (await Get(w.Owner, $"/api/analytics/teacher-utilization?{q}")).EnumerateArray().Single().GetProperty("teacherFullName").GetString());

        var dashboard = await Get(w.ManagerSmouha, $"/api/analytics/dashboard?branchId={w.Smouha}&{q}");
        Assert.Equal(w.Smouha, dashboard.GetProperty("branchId").GetGuid());

        await Pdf(w.Owner, $"/api/analytics/dashboard/export/pdf?{q}");

        var excel = await w.Owner.GetAsync($"/api/analytics/dashboard/export/excel?{q}");
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excel.Content.Headers.ContentType?.MediaType);
        Assert.Equal("PK", Encoding.ASCII.GetString((await excel.Content.ReadAsByteArrayAsync()), 0, 2));
    }
}

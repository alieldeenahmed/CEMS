using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace CEMS.Api.Tests;

/// <summary>The demo center plus the extra resources the sweep aims at, created once and shared by every test.</summary>
public class SweepWorld : ApiWorld
{
    // Resources at Smouha, reachable by Smouha staff but not by Kafr Abdo staff.
    public Guid Package, Invoice, Exam, Enrollment, Availability;

    // A second teacher at Smouha with her own course, session, exam and payroll run: not Ahmed's.
    public Guid SaraTeacher, SaraCourse, SaraSession, SaraExam, SaraAvailability, SaraRun, SaraStudent;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        var w = this;
        Package = await w.CreateAsync($"/api/courses/{w.Course}/packages", new { sessionCount = 12, price = 2400 });
        Invoice = await w.CreateAsync($"/api/students/{w.TaughtStudent}/invoices", new { packageId = Package, dueDate = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd") });
        Exam = await w.CreateAsync($"/api/courses/{w.Course}/exams", new { name = "Midterm", maxScore = 100, examDate = "2030-01-20" });
        var enrollments = await w.Owner.GetFromJsonAsync<JsonElement>($"/api/courses/{w.Course}/enrollments");
        Enrollment = enrollments[0].GetProperty("id").GetGuid();
        var availability = await w.Owner.GetFromJsonAsync<JsonElement>($"/api/teachers/{w.TeacherId}/availability");
        Availability = availability[0].GetProperty("id").GetGuid();

        var saraUser = await CreateTeacherUserAsync();
        SaraTeacher = await w.CreateAsync("/api/teachers", new { userId = saraUser, hireDate = "2024-06-01", payType = "PerSession", payRate = 100 });
        (await w.Owner.PostAsJsonAsync($"/api/teachers/{SaraTeacher}/branches", new { branchId = w.Smouha })).EnsureSuccessStatusCode();
        SaraAvailability = await w.CreateAsync($"/api/teachers/{SaraTeacher}/availability", new { branchId = w.Smouha, dayOfWeek = "Tuesday", startTime = "09:00", endTime = "17:00" });
        var curriculum = await w.CreateAsync("/api/curricula", new { name = "Robotics track", description = "Robots" });
        SaraCourse = await w.CreateAsync("/api/courses", new { name = "Robotics", deliveryMode = "Group", curriculumId = curriculum, branchId = w.Smouha });
        (await w.Owner.PostAsJsonAsync($"/api/teachers/{SaraTeacher}/qualifications", new { courseId = SaraCourse })).EnsureSuccessStatusCode();
        SaraSession = await w.CreateAsync($"/api/courses/{SaraCourse}/sessions", SessionBody(w.SmouhaRoom, SaraTeacher, SessionStart.AddDays(1)));
        SaraExam = await w.CreateAsync($"/api/courses/{SaraCourse}/exams", new { name = "Robots quiz", maxScore = 50, examDate = "2030-01-21" });
        SaraStudent = await w.CreateStudentAsync("Robot Kid", w.Smouha, w.SmouhaGuardian);
        (await w.Owner.PostAsJsonAsync($"/api/courses/{SaraCourse}/enrollments", new { studentId = SaraStudent })).EnsureSuccessStatusCode();
        SaraRun = await w.CreateAsync($"/api/teachers/{SaraTeacher}/payroll-runs", new { periodStart = "2030-01-01", periodEnd = "2030-01-31" });
    }

    private async Task<Guid> CreateTeacherUserAsync()
    {
        var response = await Owner.PostAsJsonAsync("/api/users/staff",
            new { email = "sara.sweep@codecamp.demo", password = Password, fullName = "Sara Ibrahim", phoneNumber = "01000000009", role = "Teacher", branchId = (Guid?)null });
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("userId").GetGuid();
    }
}

/// <summary>
/// A systematic probe for insecure direct object references. Rather than remembering to test each endpoint
/// by hand, this fires a request at every route that takes another resource's id -- as a signed-in user who
/// must not be able to reach that resource -- and demands 403 or 404 for all of them. A new endpoint that
/// forgets its scope check shows up here (or in the endpoint snapshot) rather than in production.
/// </summary>
public class AccessSweepTests : IClassFixture<SweepWorld>
{
    private readonly SweepWorld _w;

    public AccessSweepTests(SweepWorld world) => _w = world;

    private sealed record Probe(string Method, string Path, object? Body = null)
    {
        public override string ToString() => $"{Method} {Path}";
    }

    private static async Task<List<string>> RunAsync(HttpClient client, IEnumerable<Probe> probes)
    {
        var failures = new List<string>();
        foreach (var probe in probes)
        {
            using var request = new HttpRequestMessage(new HttpMethod(probe.Method), probe.Path);
            if (probe.Body is not null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(probe.Body), Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(request);
            if (response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.NotFound))
            {
                failures.Add($"{probe} -> {(int)response.StatusCode}");
            }
        }

        return failures;
    }

    // ---- Staff at Kafr Abdo reaching for Smouha's data ----

    private IEnumerable<Probe> SmouhaResources()
    {
        var w = _w;
        var body = new { name = "X", capacity = 5 };
        return new Probe[]
        {
            new("GET", $"/api/branches/{w.Smouha}"),
            new("GET", $"/api/branches/{w.Smouha}/rooms"),
            new("POST", $"/api/branches/{w.Smouha}/rooms", body),
            new("GET", $"/api/rooms/{w.SmouhaRoom}"),
            new("PUT", $"/api/rooms/{w.SmouhaRoom}", body),
            new("DELETE", $"/api/rooms/{w.SmouhaRoom}"),

            new("GET", $"/api/courses/{w.Course}"),
            new("PUT", $"/api/courses/{w.Course}", new { name = "Hijacked", deliveryMode = "Group", curriculumId = Guid.NewGuid(), branchId = w.Smouha }),
            new("DELETE", $"/api/courses/{w.Course}"),
            new("GET", $"/api/courses/{w.Course}/enrollments"),
            new("POST", $"/api/courses/{w.Course}/enrollments", new { studentId = w.KafrAbdoStudent }),
            new("DELETE", $"/api/courses/enrollments/{_w.Enrollment}"),
            new("POST", $"/api/courses/enrollments/{_w.Enrollment}/promote"),
            new("GET", $"/api/courses/{w.Course}/sessions"),
            new("POST", $"/api/courses/{w.Course}/sessions", ApiWorld.SessionBody(w.KafrAbdoRoom, w.TeacherId, ApiWorld.SessionStart.AddDays(14))),
            new("GET", $"/api/courses/{w.Course}/packages"),
            new("POST", $"/api/courses/{w.Course}/packages", new { sessionCount = 1, price = 1 }),
            new("GET", $"/api/courses/{w.Course}/exams"),
            new("POST", $"/api/courses/{w.Course}/exams", new { name = "Sneaky", maxScore = 10, examDate = "2030-03-01" }),

            new("GET", $"/api/sessions/{w.Session}"),
            new("GET", $"/api/sessions/{w.Session}/attendance"),
            new("POST", $"/api/sessions/{w.Session}/attendance", new { studentId = w.TaughtStudent, status = "Present" }),
            new("POST", $"/api/sessions/{w.Session}/cancel", new { rescheduledToSessionId = (Guid?)null }),
            new("POST", $"/api/sessions/{w.Session}/substitute-teacher", new { newTeacherId = _w.SaraTeacher, @override = false, overrideReason = (string?)null }),

            new("GET", $"/api/students/{w.TaughtStudent}"),
            new("PUT", $"/api/students/{w.TaughtStudent}", new { fullName = "Hijacked", dateOfBirth = "2013-04-12", gender = "Male", status = "Active" }),
            new("DELETE", $"/api/students/{w.TaughtStudent}"),
            new("GET", $"/api/students/{w.TaughtStudent}/attendance"),
            new("GET", $"/api/students/{w.TaughtStudent}/balance"),
            new("GET", $"/api/students/{w.TaughtStudent}/branch-history"),
            new("GET", $"/api/students/{w.TaughtStudent}/enrollments"),
            new("GET", $"/api/students/{w.TaughtStudent}/grades"),
            new("GET", $"/api/students/{w.TaughtStudent}/guardians"),
            new("POST", $"/api/students/{w.TaughtStudent}/guardians", new { guardianId = w.KafrAbdoGuardian, relationshipType = "Mother", isPrimaryContact = false }),
            new("DELETE", $"/api/students/{w.TaughtStudent}/guardians/{w.SmouhaGuardian}"),
            new("GET", $"/api/students/{w.TaughtStudent}/invoices"),
            new("POST", $"/api/students/{w.TaughtStudent}/invoices", new { packageId = (Guid?)null, amount = 10, dueDate = "2031-01-01" }),
            new("GET", $"/api/students/{w.TaughtStudent}/report-card"),
            new("POST", $"/api/students/{w.TaughtStudent}/transfer-branch", new { newBranchId = w.KafrAbdo, reason = "poach" }),

            new("GET", $"/api/guardians/{w.SmouhaGuardian}"),
            new("PUT", $"/api/guardians/{w.SmouhaGuardian}", new { fullName = "Hijacked", phone = "01100000000", email = "x@example.com" }),

            new("GET", $"/api/invoices/{_w.Invoice}"),
            new("GET", $"/api/invoices/{_w.Invoice}/payments"),
            new("POST", $"/api/invoices/{_w.Invoice}/payments", new { amountPaid = 1, paymentDate = DateTime.UtcNow.ToString("yyyy-MM-dd"), method = "Cash" }),
            new("POST", $"/api/invoices/{_w.Invoice}/cancel"),
            new("GET", $"/api/packages/{_w.Package}"),
            new("PUT", $"/api/packages/{_w.Package}", new { sessionCount = 1, price = 1 }),
            new("DELETE", $"/api/packages/{_w.Package}"),

            new("GET", $"/api/exams/{_w.Exam}"),
            new("PUT", $"/api/exams/{_w.Exam}", new { name = "Hijacked", maxScore = 10, examDate = "2030-03-01" }),
            new("DELETE", $"/api/exams/{_w.Exam}"),
            new("GET", $"/api/exams/{_w.Exam}/grades"),
            new("POST", $"/api/exams/{_w.Exam}/grades", new { studentId = w.TaughtStudent, score = 1, comments = "x" }),

            new("GET", $"/api/teachers/{w.TeacherId}/availability"),
            new("POST", $"/api/teachers/{w.TeacherId}/availability", new { branchId = w.Smouha, dayOfWeek = "Friday", startTime = "09:00", endTime = "10:00" }),
            new("DELETE", $"/api/teachers/availability/{_w.Availability}"),
            new("POST", $"/api/teachers/{w.TeacherId}/qualifications", new { courseId = w.Course }),
            new("DELETE", $"/api/teachers/{w.TeacherId}/qualifications/{w.Course}"),
            // (A manager may attach any teacher to their *own* branch -- teachers work across branches by design --
            // so the probe targets a branch they do not manage. Probing their own would also change the world
            // for every test that runs after this one.)
            new("POST", $"/api/teachers/{w.TeacherId}/branches", new { branchId = w.Smouha }),
            new("DELETE", $"/api/teachers/{w.TeacherId}/branches/{w.Smouha}"),
        };
    }

    [Fact]
    public async Task ABranchManagerAtKafrAbdo_CannotReachAnyOfSmouhasResources()
    {
        var failures = await RunAsync(_w.ManagerKafrAbdo, SmouhaResources());

        Assert.True(failures.Count == 0, "Reachable from another branch:\n  " + string.Join("\n  ", failures));
    }

    [Fact]
    public async Task TheFrontDeskAtKafrAbdo_CannotReachAnyOfSmouhasResources()
    {
        var failures = await RunAsync(_w.FrontDeskKafrAbdo, SmouhaResources());

        Assert.True(failures.Count == 0, "Reachable from another branch:\n  " + string.Join("\n  ", failures));
    }

    // ---- A teacher reaching for what they do not teach ----

    [Fact]
    public async Task ATeacher_CannotReachAnotherTeachersCourseSessionsExamsAvailabilityOrPay()
    {
        var w = _w;
        var probes = new Probe[]
        {
            new("GET", $"/api/sessions/{_w.SaraSession}"),
            new("GET", $"/api/sessions/{_w.SaraSession}/attendance"),
            new("POST", $"/api/sessions/{_w.SaraSession}/attendance", new { studentId = _w.SaraStudent, status = "Present" }),
            new("GET", $"/api/courses/{_w.SaraCourse}/exams"),
            new("POST", $"/api/courses/{_w.SaraCourse}/exams", new { name = "Sneaky", maxScore = 10, examDate = "2030-03-01" }),
            new("GET", $"/api/exams/{_w.SaraExam}"),
            new("PUT", $"/api/exams/{_w.SaraExam}", new { name = "Hijacked", maxScore = 10, examDate = "2030-03-01" }),
            new("DELETE", $"/api/exams/{_w.SaraExam}"),
            new("GET", $"/api/exams/{_w.SaraExam}/grades"),
            new("POST", $"/api/exams/{_w.SaraExam}/grades", new { studentId = _w.SaraStudent, score = 1, comments = "x" }),
            new("GET", $"/api/students/{_w.SaraStudent}"),
            new("GET", $"/api/students/{_w.SaraStudent}/attendance"),
            new("GET", $"/api/students/{_w.SaraStudent}/enrollments"),
            new("GET", $"/api/students/{_w.SaraStudent}/grades"),
            new("GET", $"/api/students/{w.UntaughtStudent}"),
            new("GET", $"/api/students/{w.KafrAbdoStudent}"),

            new("POST", $"/api/teachers/{_w.SaraTeacher}/availability", new { branchId = w.Smouha, dayOfWeek = "Friday", startTime = "09:00", endTime = "10:00" }),
            new("DELETE", $"/api/teachers/availability/{_w.SaraAvailability}"),
            new("GET", $"/api/teachers/{_w.SaraTeacher}/availability"),
            new("GET", $"/api/teachers/{_w.SaraTeacher}"),

            new("GET", $"/api/payroll-runs/{_w.SaraRun}"),
            new("GET", $"/api/payroll-runs/{_w.SaraRun}/line-items"),
            new("GET", $"/api/payroll-runs/{_w.SaraRun}/paystub"),
        };

        var failures = await RunAsync(w.Teacher, probes);

        Assert.True(failures.Count == 0, "A teacher could reach someone else's data:\n  " + string.Join("\n  ", failures));
    }
}

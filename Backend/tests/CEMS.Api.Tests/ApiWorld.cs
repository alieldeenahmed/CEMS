using System.Net.Http.Json;
using System.Text.Json;

namespace CEMS.Api.Tests;

/// <summary>
/// A small two-branch center built through the real API (as an Owner would), shared by the tests that
/// only read or that add their own data. Roles are represented by real signed-in clients.
/// </summary>
public class ApiWorld : CemsApiFactory
{
    public static readonly DateTime SessionStart = new(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc);   // a Monday

    public HttpClient Owner { get; private set; } = null!;
    public HttpClient ManagerSmouha { get; private set; } = null!;
    public HttpClient ManagerKafrAbdo { get; private set; } = null!;
    public HttpClient FrontDeskSmouha { get; private set; } = null!;
    public HttpClient FrontDeskKafrAbdo { get; private set; } = null!;
    public HttpClient Teacher { get; private set; } = null!;
    public HttpClient Anonymous { get; private set; } = null!;

    public Guid Smouha { get; private set; }
    public Guid KafrAbdo { get; private set; }
    public Guid TeacherId { get; private set; }
    public Guid TeacherUserId { get; private set; }
    public Guid TaughtStudent { get; private set; }
    public Guid UntaughtStudent { get; private set; }
    public Guid KafrAbdoStudent { get; private set; }
    public Guid SmouhaGuardian { get; private set; }
    public Guid KafrAbdoGuardian { get; private set; }
    public Guid SmouhaRoom { get; private set; }
    public Guid KafrAbdoRoom { get; private set; }
    public Guid Course { get; private set; }
    public Guid Session { get; private set; }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        Anonymous = CreateClient();
        Owner = await SignInAsOwnerAsync();

        Smouha = await CreateAsync("/api/branches", new { name = "CodeCamp Smouha", address = "14 Fawzy Moaz St", phone = "034567001" });
        KafrAbdo = await CreateAsync("/api/branches", new { name = "CodeCamp Kafr Abdo", address = "9 Abdel Salam Aref St", phone = "034567002" });
        SmouhaRoom = await CreateAsync($"/api/branches/{Smouha}/rooms", new { name = "Lab 1", capacity = 15 });
        KafrAbdoRoom = await CreateAsync($"/api/branches/{KafrAbdo}/rooms", new { name = "Lab 1", capacity = 15 });

        await CreateStaffAsync("bm.smouha@codecamp.demo", "Nourhan Adel", "BranchManager", Smouha);
        await CreateStaffAsync("bm.kafrabdo@codecamp.demo", "Hossam Fathy", "BranchManager", KafrAbdo);
        await CreateStaffAsync("fd.smouha@codecamp.demo", "Mariam Younis", "FrontDesk", Smouha);
        await CreateStaffAsync("fd.kafrabdo@codecamp.demo", "Youssef Ali", "FrontDesk", KafrAbdo);
        TeacherUserId = await CreateStaffAsync("teacher.smouha@codecamp.demo", "Ahmed Nabil", "Teacher", null);

        TeacherId = await CreateAsync("/api/teachers", new { userId = TeacherUserId, hireDate = "2024-01-01", payType = "Hourly", payRate = 150 });
        (await Owner.PostAsJsonAsync($"/api/teachers/{TeacherId}/branches", new { branchId = Smouha })).EnsureSuccessStatusCode();
        (await Owner.PostAsJsonAsync($"/api/teachers/{TeacherId}/availability",
            new { branchId = Smouha, dayOfWeek = "Monday", startTime = "09:00", endTime = "17:00" })).EnsureSuccessStatusCode();

        SmouhaGuardian = await CreateAsync("/api/guardians", new { fullName = "Hany Mahmoud", phone = "01123450001", email = "hany@example.com" });
        KafrAbdoGuardian = await CreateAsync("/api/guardians", new { fullName = "Doaa Kamel", phone = "01123450002", email = "doaa@example.com" });
        TaughtStudent = await CreateStudentAsync("Khaled Hany", Smouha, SmouhaGuardian);
        UntaughtStudent = await CreateStudentAsync("Lina Hany", Smouha, SmouhaGuardian);
        KafrAbdoStudent = await CreateStudentAsync("Malak Kamel", KafrAbdo, KafrAbdoGuardian);

        var curriculum = await CreateAsync("/api/curricula", new { name = "Web & Software", description = "Python and web" });
        Course = await CreateAsync("/api/courses", new { name = "Python Fundamentals", deliveryMode = "Group", curriculumId = curriculum, branchId = Smouha });
        (await Owner.PostAsJsonAsync($"/api/courses/{Course}/enrollments", new { studentId = TaughtStudent })).EnsureSuccessStatusCode();
        Session = await CreateAsync($"/api/courses/{Course}/sessions", SessionBody(SmouhaRoom, TeacherId, SessionStart));

        ManagerSmouha = await SignInAsync("bm.smouha@codecamp.demo");
        ManagerKafrAbdo = await SignInAsync("bm.kafrabdo@codecamp.demo");
        FrontDeskSmouha = await SignInAsync("fd.smouha@codecamp.demo");
        FrontDeskKafrAbdo = await SignInAsync("fd.kafrabdo@codecamp.demo");
        Teacher = await SignInAsync("teacher.smouha@codecamp.demo");
    }

    public static object SessionBody(Guid roomId, Guid teacherId, DateTime start, bool @override = false, string? reason = null) => new
    {
        roomId, teacherId, startUtc = start, endUtc = start.AddHours(1), @override, overrideReason = reason
    };

    public async Task<Guid> CreateAsync(string url, object body, HttpClient? client = null)
    {
        var response = await (client ?? Owner).PostAsJsonAsync(url, body);
        Assert.True(response.IsSuccessStatusCode, $"POST {url} failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return ReadId(await ReadJsonAsync(response));
    }

    public Task<Guid> CreateStudentAsync(string name, Guid branchId, Guid guardianId, HttpClient? client = null) =>
        CreateAsync("/api/students", new
        {
            fullName = name, dateOfBirth = "2013-04-12", gender = "Male", branchId,
            existingGuardianId = guardianId, relationshipType = "Father", isPrimaryContact = true
        }, client);

    private async Task<Guid> CreateStaffAsync(string email, string name, string role, Guid? branchId)
    {
        var response = await Owner.PostAsJsonAsync("/api/users/staff",
            new { email, password = Password, fullName = name, phoneNumber = "01000000000", role, branchId });
        Assert.True(response.IsSuccessStatusCode, $"staff {email} failed: {await response.Content.ReadAsStringAsync()}");
        return (await ReadJsonAsync(response)).GetProperty("userId").GetGuid();
    }

    private static Guid ReadId(JsonElement json) => json.GetProperty("id").GetGuid();

    /// <summary>The ids in a JSON array response, for "who can see what" assertions.</summary>
    public static async Task<HashSet<Guid>> IdsAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var json = await ReadJsonAsync(response);
        return json.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToHashSet();
    }
}

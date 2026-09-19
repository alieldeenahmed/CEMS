using System.Net;

namespace CEMS.Api.Tests;

/// <summary>
/// Calls sensitive endpoints as every role and checks the gate. "Passes the gate" means anything other
/// than 401/403 (the request may still be rejected later, e.g. for a missing body). Authorization runs
/// before model binding, so no valid payload is needed to prove a role is turned away.
/// </summary>
public class AuthorizationMatrixTests : IClassFixture<ApiWorld>
{
    private readonly ApiWorld _w;

    public AuthorizationMatrixTests(ApiWorld world) => _w = world;

    private HttpClient As(string role) => role switch
    {
        "Owner" => _w.Owner,
        "BranchManager" => _w.ManagerSmouha,
        "FrontDesk" => _w.FrontDeskSmouha,
        "Teacher" => _w.Teacher,
        "Anonymous" => _w.Anonymous,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private string Fill(string path) => path
        .Replace("{smouha}", _w.Smouha.ToString())
        .Replace("{kafr}", _w.KafrAbdo.ToString())
        .Replace("{student}", _w.TaughtStudent.ToString())
        .Replace("{teacher}", _w.TeacherId.ToString())
        .Replace("{teacherUser}", _w.TeacherUserId.ToString())
        .Replace("{course}", _w.Course.ToString())
        .Replace("{session}", _w.Session.ToString())
        .Replace("{any}", Guid.NewGuid().ToString());

    private async Task<HttpStatusCode> Call(string role, string verb, string path) =>
        (await As(role).SendAsync(new HttpRequestMessage(new HttpMethod(verb), Fill(path)))).StatusCode;

    private const string Window = "periodStart=2030-01-01&periodEnd=2030-01-31";

    // Roles that must be turned away (403). Each row: role, verb, path.
    public static TheoryData<string, string, string> Denied => new()
    {
        // Money and payroll are Owner-only.
        { "FrontDesk", "POST", "/api/teachers/{teacher}/payroll-runs" },
        { "BranchManager", "POST", "/api/teachers/{teacher}/payroll-runs" },
        { "Teacher", "POST", "/api/teachers/{teacher}/payroll-runs" },
        { "BranchManager", "GET", "/api/teachers/{teacher}/payroll-runs" },
        { "FrontDesk", "POST", "/api/payroll-runs/{any}/approve" },
        { "BranchManager", "POST", "/api/payroll-runs/{any}/mark-paid" },
        { "BranchManager", "POST", "/api/users/staff/{any}/payroll-runs" },
        { "FrontDesk", "PUT", "/api/staff-payroll-runs/{any}" },
        { "BranchManager", "GET", "/api/payroll-runs/{any}" },
        { "FrontDesk", "GET", "/api/payroll-runs/{any}/paystub" },
        // Account administration.
        { "BranchManager", "GET", "/api/users/staff" },
        { "FrontDesk", "GET", "/api/users/staff" },
        { "BranchManager", "PUT", "/api/users/staff/{teacherUser}/status" },
        { "FrontDesk", "POST", "/api/users/staff" },
        { "Teacher", "POST", "/api/users/staff/{teacherUser}/reset-password" },
        { "FrontDesk", "POST", "/api/users/staff/{teacherUser}/reset-password" },
        // Organization structure.
        { "BranchManager", "POST", "/api/branches" },
        { "FrontDesk", "DELETE", "/api/branches/{smouha}" },
        { "BranchManager", "PUT", "/api/branches/{smouha}" },
        { "BranchManager", "DELETE", "/api/teachers/{teacher}" },
        { "BranchManager", "PUT", "/api/teachers/{teacher}" },
        { "FrontDesk", "POST", "/api/teachers" },
        // Front desk works with people and money at the desk, not the academic or reporting side.
        { "FrontDesk", "GET", "/api/analytics/revenue?" + Window },
        { "FrontDesk", "POST", "/api/courses" },
        { "FrontDesk", "POST", "/api/invoices/{any}/cancel" },
        { "FrontDesk", "POST", "/api/exams/{any}/grades" },
        { "FrontDesk", "POST", "/api/sessions/{session}/attendance" },
        { "FrontDesk", "POST", "/api/teachers/{teacher}/qualifications" },
        // Teachers are limited to their own classroom work.
        { "Teacher", "GET", "/api/students" },
        { "Teacher", "GET", "/api/branches" },
        { "Teacher", "GET", "/api/guardians" },
        { "Teacher", "GET", "/api/teachers" },
        { "Teacher", "GET", "/api/analytics/revenue?" + Window },
        { "Teacher", "POST", "/api/invoices/{any}/payments" },
        { "Teacher", "GET", "/api/students/{student}/invoices" },
        { "Teacher", "GET", "/api/students/{student}/report-card" },
        { "Teacher", "POST", "/api/students/{student}/transfer-branch" },
        { "Teacher", "POST", "/api/courses/{course}/sessions" },
        { "Teacher", "POST", "/api/courses/{course}/enrollments" },
        { "BranchManager", "GET", "/api/teachers/my-profile" },
        { "FrontDesk", "GET", "/api/courses/my-courses" },
        // Branch-manager-only view.
        { "Owner", "GET", "/api/users/staff/my-branch" },
        { "FrontDesk", "GET", "/api/users/staff/my-branch" },
        // Signed out.
        { "Anonymous", "GET", "/api/students" },
        { "Anonymous", "POST", "/api/students/{student}/transfer-branch" },
    };

    // Roles that must get past the gate.
    public static TheoryData<string, string, string> Allowed => new()
    {
        { "Owner", "GET", "/api/users/staff" },
        { "Owner", "GET", "/api/teachers/{teacher}/payroll-runs" },
        { "Owner", "GET", "/api/analytics/revenue?" + Window },
        { "BranchManager", "GET", "/api/users/staff/my-branch" },
        { "BranchManager", "GET", "/api/analytics/revenue?branchId={smouha}&" + Window },
        { "BranchManager", "GET", "/api/teachers/candidates" },
        { "BranchManager", "POST", "/api/users/staff/{teacherUser}/reset-password" },
        { "FrontDesk", "GET", "/api/students" },
        { "FrontDesk", "GET", "/api/courses" },
        { "FrontDesk", "POST", "/api/invoices/{any}/payments" },
        { "FrontDesk", "POST", "/api/courses/{course}/sessions" },
        { "FrontDesk", "POST", "/api/students/{student}/transfer-branch" },
        { "Teacher", "GET", "/api/teachers/my-profile" },
        { "Teacher", "GET", "/api/teachers/my-schedule" },
        { "Teacher", "GET", "/api/courses/my-courses" },
        { "Teacher", "GET", "/api/teachers/my-payroll-runs" },
        { "Teacher", "GET", "/api/sessions/{session}/attendance" },
        { "Teacher", "POST", "/api/courses/{course}/exams" },
        { "Teacher", "GET", "/api/curricula" },
    };

    [Theory]
    [MemberData(nameof(Denied))]
    public async Task TheRole_IsTurnedAway(string role, string verb, string path)
    {
        var status = await Call(role, verb, path);

        var expected = role == "Anonymous" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
        Assert.True(status == expected, $"{role} {verb} {path} returned {(int)status}, expected {(int)expected}");
    }

    [Theory]
    [MemberData(nameof(Allowed))]
    public async Task TheRole_PassesTheGate(string role, string verb, string path)
    {
        var status = await Call(role, verb, path);

        Assert.True(status != HttpStatusCode.Unauthorized && status != HttpStatusCode.Forbidden,
            $"{role} {verb} {path} was turned away with {(int)status}");
    }
}

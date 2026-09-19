using CEMS.Application.Common.Exceptions;
using CEMS.Application.Courses.Queries.GetEnrollmentsForStudent;
using CEMS.Application.Payments.Queries.GetInvoicesForStudent;
using CEMS.Application.Payments.Queries.GetOutstandingBalanceForStudent;
using CEMS.Application.Students.Commands.CreateStudent;
using CEMS.Application.Students.Commands.DeleteStudent;
using CEMS.Application.Students.Commands.LinkGuardian;
using CEMS.Application.Students.Commands.UnlinkGuardian;
using CEMS.Application.Students.Commands.UpdateStudent;
using CEMS.Application.Students.Queries.GetBranchHistoryForStudent;
using CEMS.Application.Students.Queries.GetGuardiansForStudent;
using CEMS.Application.Students.Queries.GetStudentById;
using CEMS.Application.Students.Queries.GetStudents;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Payments;
using CEMS.Domain.Students;
using CEMS.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Application.Tests.Students;

public class StudentHandlerTests : SeededHandlerTestBase
{
    private readonly Branch _smouha;
    private readonly Branch _kafrAbdo;

    public StudentHandlerTests()
    {
        _smouha = AddBranch("Smouha");
        _kafrAbdo = AddBranch("Kafr Abdo");
    }

    private static CreateStudentCommand NewStudent(Guid branchId, Guid? existingGuardianId = null, string? guardianName = "Doaa Kamel") =>
        new("Malak Kamel", new DateOnly(2012, 11, 2), Gender.Female, branchId, existingGuardianId,
            existingGuardianId is null ? guardianName : null,
            existingGuardianId is null ? "01123450002" : null,
            existingGuardianId is null ? "doaa@example.com" : null,
            RelationshipType.Mother, true);

    private Task<CEMS.Application.Students.StudentDto> Create(CreateStudentCommand command) =>
        new CreateStudentCommandHandler(Context, CurrentUser).Handle(command, CancellationToken.None);

    // ---- CreateStudent ----

    [Fact]
    public async Task Create_WithNewGuardian_WritesStudentGuardianAndLinkTogether()
    {
        ActAs(RoleNames.FrontDesk, _smouha);

        var dto = await Create(NewStudent(_smouha.Id));

        Assert.Equal(_smouha.Id, dto.CurrentBranchId);
        var link = Assert.Single(Context.StudentGuardians.Include(sg => sg.Guardian));
        Assert.Equal(dto.Id, link.StudentId);
        Assert.Equal("Doaa Kamel", link.Guardian.FullName);
        Assert.True(link.IsPrimaryContact);
    }

    [Fact]
    public async Task Create_WithExistingGuardian_ReusesItInsteadOfDuplicating()
    {
        ActAs(RoleNames.FrontDesk, _smouha);
        var guardian = AddGuardian();

        await Create(NewStudent(_smouha.Id, guardian.Id));

        Assert.Single(Context.Guardians);
        Assert.Equal(guardian.Id, Assert.Single(Context.StudentGuardians).GuardianId);
    }

    [Fact]
    public async Task Create_WithAGuardianFromAnotherBranch_IsReportedAsNotFound()
    {
        var kafrAbdoStudent = AddStudent(_kafrAbdo);
        var foreignGuardian = AddGuardian("Foreign Parent");
        Context.StudentGuardians.Add(new StudentGuardian { StudentId = kafrAbdoStudent.Id, GuardianId = foreignGuardian.Id, RelationshipType = RelationshipType.Father, IsPrimaryContact = true });
        Context.SaveChanges();
        ActAs(RoleNames.FrontDesk, _smouha);

        await Assert.ThrowsAsync<NotFoundException>(() => Create(NewStudent(_smouha.Id, foreignGuardian.Id)));

        Assert.Empty(Context.Students.Where(s => s.FullName == "Malak Kamel"));
    }

    [Fact]
    public async Task Create_AtABranchTheUserDoesNotBelongTo_ThrowsForbidden()
    {
        ActAs(RoleNames.FrontDesk, _smouha);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Create(NewStudent(_kafrAbdo.Id)));
    }

    [Fact]
    public async Task Create_AtAnUnknownBranch_ThrowsNotFound()
    {
        ActAs(RoleNames.Owner);

        await Assert.ThrowsAsync<NotFoundException>(() => Create(NewStudent(Guid.NewGuid())));
    }

    [Fact]
    public async Task Create_WithNoGuardianAtAll_IsRejectedByTheValidator()
    {
        var command = NewStudent(_smouha.Id, guardianName: null);

        var result = new CreateStudentCommandValidator().Validate(command);

        Assert.False(result.IsValid);
    }

    // ---- Reads: branch scoping ----

    [Fact]
    public async Task GetStudents_BranchUsersSeeOnlyTheirBranch_OwnerSeesAll()
    {
        AddStudent(_smouha, "Zed Smouha");
        AddStudent(_smouha, "Amr Smouha");
        AddStudent(_kafrAbdo, "Rana Kafr Abdo");

        ActAs(RoleNames.FrontDesk, _smouha);
        var smouhaView = await new GetStudentsQueryHandler(Context, CurrentUser).Handle(new GetStudentsQuery(), CancellationToken.None);

        ActAs(RoleNames.Owner);
        var ownerView = await new GetStudentsQueryHandler(Context, CurrentUser).Handle(new GetStudentsQuery(), CancellationToken.None);

        Assert.Equal(["Amr Smouha", "Zed Smouha"], smouhaView.Select(s => s.FullName).ToArray());
        Assert.Equal(3, ownerView.Count);
    }

    [Fact]
    public async Task GetStudentById_OtherBranchIsForbidden_UnknownIsNotFound()
    {
        var student = AddStudent(_kafrAbdo);
        ActAs(RoleNames.BranchManager, _smouha);
        var handler = new GetStudentByIdQueryHandler(Context, CurrentUser);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new GetStudentByIdQuery(student.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetStudentByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetStudentById_ATeacherCanOpenOnlyStudentsTheyActuallyTeach()
    {
        var taught = AddStudent(_smouha, "Taught");
        var notTaught = AddStudent(_smouha, "Not Taught");
        var course = AddCourse(_smouha);
        var teacher = AddTeacher(_smouha);
        AddSession(course, AddRoom(_smouha), teacher, new DateTime(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc));
        Enroll(taught, course);
        CurrentUser.UserId = teacher.UserId;
        CurrentUser.Roles = [RoleNames.Teacher];
        CurrentUser.BranchIds = [];
        var handler = new GetStudentByIdQueryHandler(Context, CurrentUser);

        var result = await handler.Handle(new GetStudentByIdQuery(taught.Id), CancellationToken.None);

        Assert.Equal("Taught", result.FullName);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new GetStudentByIdQuery(notTaught.Id), CancellationToken.None));
    }

    // ---- Update / Delete ----

    [Fact]
    public async Task Update_ChangesTheStudentButNotTheirBranch()
    {
        var student = AddStudent(_smouha);
        ActAs(RoleNames.FrontDesk, _smouha);

        var result = await new UpdateStudentCommandHandler(Context, CurrentUser)
            .Handle(new UpdateStudentCommand(student.Id, "Khaled H.", new DateOnly(2013, 5, 1), Gender.Male, StudentStatus.Paused), CancellationToken.None);

        Assert.Equal("Khaled H.", result.FullName);
        Assert.Equal(StudentStatus.Paused, result.Status);
        Assert.Equal(_smouha.Id, result.CurrentBranchId);
    }

    [Fact]
    public async Task Update_FromAnotherBranch_ThrowsForbiddenAndChangesNothing()
    {
        var student = AddStudent(_kafrAbdo, "Original");
        ActAs(RoleNames.FrontDesk, _smouha);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new UpdateStudentCommandHandler(Context, CurrentUser)
                .Handle(new UpdateStudentCommand(student.Id, "Hacked", new DateOnly(2013, 5, 1), Gender.Male, StudentStatus.Active), CancellationToken.None));

        Assert.Equal("Original", Context.Students.Single(s => s.Id == student.Id).FullName);
    }

    [Fact]
    public async Task Delete_RemovesAStudentWithNoHistory_AndTheirGuardianLinks()
    {
        var student = AddStudent(_smouha);
        var guardian = AddGuardian();
        Context.StudentGuardians.Add(new StudentGuardian { StudentId = student.Id, GuardianId = guardian.Id, RelationshipType = RelationshipType.Father });
        Context.SaveChanges();
        ActAs(RoleNames.FrontDesk, _smouha);

        await new DeleteStudentCommandHandler(Context, CurrentUser).Handle(new DeleteStudentCommand(student.Id), CancellationToken.None);

        Assert.Empty(Context.Students);
        Assert.Empty(Context.StudentGuardians);
        Assert.Single(Context.Guardians);
    }

    [Theory]
    [InlineData("enrollment")]
    [InlineData("invoice")]
    public async Task Delete_AStudentWithRecordsOnFile_ThrowsBadRequestInsteadOfFailingInTheDatabase(string kind)
    {
        // Regression: this used to surface as an unhandled InvalidOperationException (an HTTP 500).
        var student = AddStudent(_smouha);
        if (kind == "enrollment")
        {
            Enroll(student, AddCourse(_smouha));
        }
        else
        {
            AddInvoice(student, 100);
        }

        ActAs(RoleNames.Owner);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            new DeleteStudentCommandHandler(Context, CurrentUser).Handle(new DeleteStudentCommand(student.Id), CancellationToken.None));

        Assert.Contains("Paused or Graduated", ex.Errors[0]);
        Assert.Single(Context.Students);
    }

    [Fact]
    public async Task Delete_FromAnotherBranch_ThrowsForbidden()
    {
        var student = AddStudent(_kafrAbdo);
        ActAs(RoleNames.FrontDesk, _smouha);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new DeleteStudentCommandHandler(Context, CurrentUser).Handle(new DeleteStudentCommand(student.Id), CancellationToken.None));

        Assert.Single(Context.Students);
    }

    // ---- Guardian links ----

    [Fact]
    public async Task LinkGuardian_AddsTheLink_AndRejectsALinkThatAlreadyExists()
    {
        var student = AddStudent(_smouha);
        var guardian = AddGuardian();
        ActAs(RoleNames.FrontDesk, _smouha);
        var handler = new LinkGuardianCommandHandler(Context, CurrentUser);
        var command = new LinkGuardianCommand(student.Id, guardian.Id, RelationshipType.Mother, false);

        await handler.Handle(command, CancellationToken.None);

        Assert.Single(Context.StudentGuardians);
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task LinkGuardian_ToAStudentAtAnotherBranch_ThrowsForbidden()
    {
        var student = AddStudent(_kafrAbdo);
        var guardian = AddGuardian();
        ActAs(RoleNames.FrontDesk, _smouha);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new LinkGuardianCommandHandler(Context, CurrentUser)
                .Handle(new LinkGuardianCommand(student.Id, guardian.Id, RelationshipType.Mother, false), CancellationToken.None));
    }

    [Fact]
    public async Task LinkGuardian_UnknownGuardian_ThrowsNotFound()
    {
        var student = AddStudent(_smouha);
        ActAs(RoleNames.FrontDesk, _smouha);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new LinkGuardianCommandHandler(Context, CurrentUser)
                .Handle(new LinkGuardianCommand(student.Id, Guid.NewGuid(), RelationshipType.Mother, false), CancellationToken.None));
    }

    [Fact]
    public async Task UnlinkGuardian_RemovesOnlyTheLink_NotTheGuardian()
    {
        var student = AddStudent(_smouha);
        var guardian = AddGuardian();
        Context.StudentGuardians.Add(new StudentGuardian { StudentId = student.Id, GuardianId = guardian.Id, RelationshipType = RelationshipType.Father });
        Context.SaveChanges();
        ActAs(RoleNames.FrontDesk, _smouha);

        await new UnlinkGuardianCommandHandler(Context, CurrentUser).Handle(new UnlinkGuardianCommand(student.Id, guardian.Id), CancellationToken.None);

        Assert.Empty(Context.StudentGuardians);
        Assert.Single(Context.Guardians);
    }

    [Fact]
    public async Task UnlinkGuardian_MissingLink_ThrowsNotFound_AndOtherBranchThrowsForbidden()
    {
        var student = AddStudent(_kafrAbdo);
        var guardian = AddGuardian();
        Context.StudentGuardians.Add(new StudentGuardian { StudentId = student.Id, GuardianId = guardian.Id, RelationshipType = RelationshipType.Father });
        Context.SaveChanges();
        ActAs(RoleNames.FrontDesk, _smouha);
        var handler = new UnlinkGuardianCommandHandler(Context, CurrentUser);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new UnlinkGuardianCommand(student.Id, Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new UnlinkGuardianCommand(student.Id, guardian.Id), CancellationToken.None));
    }

    [Fact]
    public async Task GetGuardiansForStudent_ReturnsThatStudentsGuardians_ForAuthorizedUsersOnly()
    {
        var student = AddStudent(_smouha);
        var guardian = AddGuardian("Hany Mahmoud");
        Context.StudentGuardians.Add(new StudentGuardian { StudentId = student.Id, GuardianId = guardian.Id, RelationshipType = RelationshipType.Father });
        Context.SaveChanges();

        ActAs(RoleNames.FrontDesk, _smouha);
        var result = await new GetGuardiansForStudentQueryHandler(Context, CurrentUser).Handle(new GetGuardiansForStudentQuery(student.Id), CancellationToken.None);
        Assert.Equal("Hany Mahmoud", Assert.Single(result).FullName);

        ActAs(RoleNames.FrontDesk, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetGuardiansForStudentQueryHandler(Context, CurrentUser).Handle(new GetGuardiansForStudentQuery(student.Id), CancellationToken.None));
    }

    // ---- Branch history ----

    [Fact]
    public async Task GetBranchHistory_ReturnsMostRecentTransferFirst_AndIsBranchScoped()
    {
        var student = AddStudent(_smouha);
        Context.StudentBranchHistories.AddRange(
            new StudentBranchHistory { Id = Guid.NewGuid(), StudentId = student.Id, FromBranchId = _kafrAbdo.Id, ToBranchId = _smouha.Id, TransferDate = new DateOnly(2025, 1, 1), TransferredByUserId = Guid.NewGuid() },
            new StudentBranchHistory { Id = Guid.NewGuid(), StudentId = student.Id, FromBranchId = _smouha.Id, ToBranchId = _kafrAbdo.Id, TransferDate = new DateOnly(2024, 1, 1), TransferredByUserId = Guid.NewGuid() });
        Context.SaveChanges();

        ActAs(RoleNames.BranchManager, _smouha);
        var result = await new GetBranchHistoryForStudentQueryHandler(Context, CurrentUser)
            .Handle(new GetBranchHistoryForStudentQuery(student.Id), CancellationToken.None);

        Assert.Equal([new DateOnly(2025, 1, 1), new DateOnly(2024, 1, 1)], result.Select(h => h.TransferDate).ToArray());
        Assert.Equal("Kafr Abdo", result[0].FromBranchName);

        ActAs(RoleNames.BranchManager, AddBranch("Elsewhere"));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetBranchHistoryForStudentQueryHandler(Context, CurrentUser).Handle(new GetBranchHistoryForStudentQuery(student.Id), CancellationToken.None));
    }

    // ---- Student-scoped billing and enrollment reads ----

    [Fact]
    public async Task OutstandingBalance_SumsUnpaidAmountsAcrossInvoices_IgnoringCancelledOnes()
    {
        var student = AddStudent(_smouha);
        var partial = AddInvoice(student, 1000, InvoiceStatus.PartiallyPaid);
        AddPayment(partial, 300);
        AddInvoice(student, 500);
        AddInvoice(student, 9999, InvoiceStatus.Cancelled);
        ActAs(RoleNames.FrontDesk, _smouha);

        var result = await new GetOutstandingBalanceForStudentQueryHandler(Context, CurrentUser)
            .Handle(new GetOutstandingBalanceForStudentQuery(student.Id), CancellationToken.None);

        Assert.Equal(1200, result.TotalOutstanding);
    }

    [Fact]
    public async Task OutstandingBalance_FromAnotherBranch_ThrowsForbidden()
    {
        var student = AddStudent(_kafrAbdo);
        ActAs(RoleNames.FrontDesk, _smouha);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetOutstandingBalanceForStudentQueryHandler(Context, CurrentUser).Handle(new GetOutstandingBalanceForStudentQuery(student.Id), CancellationToken.None));
    }

    [Fact]
    public async Task InvoicesForStudent_ComputesPaidBalanceAndOverdue_NewestFirst()
    {
        var student = AddStudent(_smouha);
        var overdue = AddInvoice(student, 800, InvoiceStatus.PartiallyPaid, due: new DateOnly(2020, 1, 1));
        AddPayment(overdue, 200);
        var current = AddInvoice(student, 400);
        current.IssuedDate = new DateOnly(2026, 1, 1);
        Context.SaveChanges();
        ActAs(RoleNames.FrontDesk, _smouha);

        var result = await new GetInvoicesForStudentQueryHandler(Context, CurrentUser)
            .Handle(new GetInvoicesForStudentQuery(student.Id), CancellationToken.None);

        Assert.Equal([current.Id, overdue.Id], result.Select(r => r.Id).ToArray());
        var overdueDto = result.Single(r => r.Id == overdue.Id);
        Assert.Equal(200, overdueDto.AmountPaid);
        Assert.Equal(600, overdueDto.BalanceRemaining);
        Assert.True(overdueDto.IsOverdue);
        Assert.False(result.Single(r => r.Id == current.Id).IsOverdue);
    }

    [Fact]
    public async Task EnrollmentsForStudent_StaffSeeAll_ATeacherSeesOnlyTheirOwnCourses()
    {
        var student = AddStudent(_smouha);
        var teacher = AddTeacher(_smouha);
        var room = AddRoom(_smouha);
        var mine = AddCourse(_smouha, name: "Python");
        var others = AddCourse(_smouha, name: "Scratch");
        AddSession(mine, room, teacher, new DateTime(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc));
        Enroll(student, mine);
        Enroll(student, others);

        ActAs(RoleNames.FrontDesk, _smouha);
        var staffView = await new GetEnrollmentsForStudentQueryHandler(Context, CurrentUser)
            .Handle(new GetEnrollmentsForStudentQuery(student.Id), CancellationToken.None);

        CurrentUser.UserId = teacher.UserId;
        CurrentUser.Roles = [RoleNames.Teacher];
        CurrentUser.BranchIds = [];
        var teacherView = await new GetEnrollmentsForStudentQueryHandler(Context, CurrentUser)
            .Handle(new GetEnrollmentsForStudentQuery(student.Id), CancellationToken.None);

        Assert.Equal(2, staffView.Count);
        Assert.Equal("Python", Assert.Single(teacherView).CourseName);
    }

    [Fact]
    public async Task EnrollmentsForStudent_ATeacherWhoDoesNotTeachThemIsForbidden()
    {
        var student = AddStudent(_smouha);
        var stranger = AddTeacher(_smouha);
        CurrentUser.UserId = stranger.UserId;
        CurrentUser.Roles = [RoleNames.Teacher];
        CurrentUser.BranchIds = [];

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetEnrollmentsForStudentQueryHandler(Context, CurrentUser).Handle(new GetEnrollmentsForStudentQuery(student.Id), CancellationToken.None));
    }
}

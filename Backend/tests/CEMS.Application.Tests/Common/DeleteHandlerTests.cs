using CEMS.Application.Branches.Commands.DeleteBranch;
using CEMS.Application.Branches.Rooms.Commands.DeleteRoom;
using CEMS.Application.Common.Exceptions;
using CEMS.Application.Courses.Commands.DeleteCourse;
using CEMS.Application.Courses.Commands.DeleteCurriculum;
using CEMS.Application.Exams.Commands.DeleteExam;
using CEMS.Application.Payments.Commands.DeletePackage;
using CEMS.Application.Teachers.Commands.DeleteTeacher;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Exams;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Common;

/// <summary>
/// A delete that the database refuses because other records depend on the row has to come back as a
/// readable 400, never an unhandled exception (a 500). Each test builds a row with a dependent, then
/// checks both that the guard fires and that nothing was removed; the clean-delete cases prove the
/// guard doesn't over-block.
/// </summary>
public class DeleteHandlerTests : SeededHandlerTestBase
{
    private static readonly DateTime NextMonday = new(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc);

    // ---- Branch ----

    [Fact]
    public async Task Branch_WithRooms_ThrowsBadRequestAndKeepsTheBranch()
    {
        var branch = AddBranch();
        AddRoom(branch);

        Context.ChangeTracker.Clear();
        await Assert.ThrowsAsync<BadRequestException>(() =>
            new DeleteBranchCommandHandler(Context).Handle(new DeleteBranchCommand(branch.Id), CancellationToken.None));

        Assert.Single(Context.Branches);
    }

    [Fact]
    public async Task Branch_Empty_IsDeleted_AndUnknownIsNotFound()
    {
        var branch = AddBranch();
        var handler = new DeleteBranchCommandHandler(Context);

        Context.ChangeTracker.Clear();
        await handler.Handle(new DeleteBranchCommand(branch.Id), CancellationToken.None);

        Assert.Empty(Context.Branches);
        Context.ChangeTracker.Clear();
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new DeleteBranchCommand(Guid.NewGuid()), CancellationToken.None));
    }

    // ---- Room ----

    [Fact]
    public async Task Room_WithScheduledSessions_ThrowsBadRequestAndKeepsTheRoom()
    {
        var branch = AddBranch();
        var room = AddRoom(branch);
        AddSession(AddCourse(branch), room, AddTeacher(branch), NextMonday);
        ActAs(RoleNames.Owner);

        Context.ChangeTracker.Clear();
        await Assert.ThrowsAsync<BadRequestException>(() =>
            new DeleteRoomCommandHandler(Context, CurrentUser).Handle(new DeleteRoomCommand(room.Id), CancellationToken.None));

        Assert.Single(Context.Rooms);
    }

    [Fact]
    public async Task Room_Unused_IsDeleted_ButNotByAnotherBranchsManager()
    {
        var mine = AddBranch("Smouha");
        var other = AddBranch("Kafr Abdo");
        var room = AddRoom(mine);

        ActAs(RoleNames.BranchManager, other);
        Context.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new DeleteRoomCommandHandler(Context, CurrentUser).Handle(new DeleteRoomCommand(room.Id), CancellationToken.None));

        ActAs(RoleNames.BranchManager, mine);
        Context.ChangeTracker.Clear();
        await new DeleteRoomCommandHandler(Context, CurrentUser).Handle(new DeleteRoomCommand(room.Id), CancellationToken.None);
        Assert.Empty(Context.Rooms);
    }

    // ---- Course ----

    [Fact]
    public async Task Course_WithEnrollments_ThrowsBadRequestAndKeepsTheCourse()
    {
        var branch = AddBranch();
        var course = AddCourse(branch);
        Enroll(AddStudent(branch), course);
        ActAs(RoleNames.Owner);

        Context.ChangeTracker.Clear();
        await Assert.ThrowsAsync<BadRequestException>(() =>
            new DeleteCourseCommandHandler(Context, CurrentUser).Handle(new DeleteCourseCommand(course.Id), CancellationToken.None));

        Assert.Single(Context.Courses);
    }

    [Fact]
    public async Task Course_Unused_IsDeleted_ButNotByAnotherBranchsManager()
    {
        var mine = AddBranch("Smouha");
        var other = AddBranch("Kafr Abdo");
        var course = AddCourse(mine);

        ActAs(RoleNames.BranchManager, other);
        Context.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new DeleteCourseCommandHandler(Context, CurrentUser).Handle(new DeleteCourseCommand(course.Id), CancellationToken.None));

        ActAs(RoleNames.BranchManager, mine);
        Context.ChangeTracker.Clear();
        await new DeleteCourseCommandHandler(Context, CurrentUser).Handle(new DeleteCourseCommand(course.Id), CancellationToken.None);
        Assert.Empty(Context.Courses);
    }

    // ---- Curriculum ----

    [Fact]
    public async Task Curriculum_WithCourses_ThrowsBadRequestAndKeepsTheCurriculum()
    {
        var curriculum = AddCurriculum();
        AddCourse(AddBranch(), curriculum);

        Context.ChangeTracker.Clear();
        await Assert.ThrowsAsync<BadRequestException>(() =>
            new DeleteCurriculumCommandHandler(Context).Handle(new DeleteCurriculumCommand(curriculum.Id), CancellationToken.None));

        Assert.Single(Context.Curricula);
    }

    [Fact]
    public async Task Curriculum_Empty_IsDeleted()
    {
        var curriculum = AddCurriculum();

        Context.ChangeTracker.Clear();
        await new DeleteCurriculumCommandHandler(Context).Handle(new DeleteCurriculumCommand(curriculum.Id), CancellationToken.None);

        Assert.Empty(Context.Curricula);
    }

    // ---- Teacher ----

    [Fact]
    public async Task Teacher_WithScheduledSessions_ThrowsBadRequestAndKeepsTheTeacher()
    {
        var branch = AddBranch();
        var teacher = AddTeacher(branch);
        AddSession(AddCourse(branch), AddRoom(branch), teacher, NextMonday);

        Context.ChangeTracker.Clear();
        await Assert.ThrowsAsync<BadRequestException>(() =>
            new DeleteTeacherCommandHandler(Context).Handle(new DeleteTeacherCommand(teacher.Id), CancellationToken.None));

        Assert.Single(Context.Teachers);
    }

    [Fact]
    public async Task Teacher_WithOnlyBranchAssignments_IsDeleted()
    {
        var teacher = AddTeacher(AddBranch());

        Context.ChangeTracker.Clear();
        await new DeleteTeacherCommandHandler(Context).Handle(new DeleteTeacherCommand(teacher.Id), CancellationToken.None);

        Assert.Empty(Context.Teachers);
    }

    // ---- Package ----

    [Fact]
    public async Task Package_WithInvoicesIssued_ThrowsBadRequestAndKeepsThePackage()
    {
        var branch = AddBranch();
        var package = AddPackage(AddCourse(branch));
        var invoice = AddInvoice(AddStudent(branch), 100);
        invoice.PackageId = package.Id;
        Context.SaveChanges();
        ActAs(RoleNames.Owner);

        Context.ChangeTracker.Clear();
        await Assert.ThrowsAsync<BadRequestException>(() =>
            new DeletePackageCommandHandler(Context, CurrentUser).Handle(new DeletePackageCommand(package.Id), CancellationToken.None));

        Assert.Single(Context.Packages);
    }

    [Fact]
    public async Task Package_Unused_IsDeleted()
    {
        var package = AddPackage(AddCourse(AddBranch()));
        ActAs(RoleNames.Owner);

        Context.ChangeTracker.Clear();
        await new DeletePackageCommandHandler(Context, CurrentUser).Handle(new DeletePackageCommand(package.Id), CancellationToken.None);

        Assert.Empty(Context.Packages);
    }

    // ---- Exam ----

    [Fact]
    public async Task Exam_WithRecordedGrades_IsDeletedTogetherWithThem_ByAuthorizedUsersOnly()
    {
        var mine = AddBranch("Smouha");
        var other = AddBranch("Kafr Abdo");
        var course = AddCourse(mine);
        var exam = AddExam(course);
        Context.Grades.Add(new Grade { Id = Guid.NewGuid(), ExamId = exam.Id, StudentId = AddStudent(mine).Id, Score = 80 });
        Context.SaveChanges();

        ActAs(RoleNames.BranchManager, other);
        Context.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new DeleteExamCommandHandler(Context, CurrentUser).Handle(new DeleteExamCommand(exam.Id), CancellationToken.None));

        ActAs(RoleNames.BranchManager, mine);
        Context.ChangeTracker.Clear();
        await new DeleteExamCommandHandler(Context, CurrentUser).Handle(new DeleteExamCommand(exam.Id), CancellationToken.None);

        Assert.Empty(Context.Exams);
        Assert.Empty(Context.Grades);
    }
}

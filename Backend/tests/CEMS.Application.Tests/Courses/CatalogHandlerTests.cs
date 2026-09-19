using CEMS.Application.Branches.Commands.UpdateBranch;
using CEMS.Application.Branches.Queries.GetBranchById;
using CEMS.Application.Branches.Queries.GetBranches;
using CEMS.Application.Branches.Rooms.Commands.CreateRoom;
using CEMS.Application.Branches.Rooms.Commands.UpdateRoom;
using CEMS.Application.Branches.Rooms.Queries.GetRoomById;
using CEMS.Application.Branches.Rooms.Queries.GetRoomsByBranch;
using CEMS.Application.Common.Exceptions;
using CEMS.Application.Courses.Commands.CreateCourse;
using CEMS.Application.Courses.Commands.CreateCurriculum;
using CEMS.Application.Courses.Commands.DropEnrollment;
using CEMS.Application.Courses.Commands.UpdateCourse;
using CEMS.Application.Courses.Commands.UpdateCurriculum;
using CEMS.Application.Courses.Queries.GetCourseById;
using CEMS.Application.Courses.Queries.GetCourses;
using CEMS.Application.Courses.Queries.GetCurricula;
using CEMS.Application.Courses.Queries.GetCurriculumById;
using CEMS.Application.Courses.Queries.GetEnrollmentsForCourse;
using CEMS.Application.Courses.Queries.GetMyCourses;
using CEMS.Application.Payments.Commands.CreateInvoice;
using CEMS.Application.Payments.Commands.CreatePackage;
using CEMS.Application.Payments.Commands.UpdatePackage;
using CEMS.Application.Payments.Queries.GetPackageById;
using CEMS.Application.Payments.Queries.GetPackagesForCourse;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Courses;

/// <summary>Branch-scoped catalog data: branches, rooms, courses, curricula and billing packages.</summary>
public class CatalogHandlerTests : SeededHandlerTestBase
{
    private readonly Branch _smouha;
    private readonly Branch _kafrAbdo;

    public CatalogHandlerTests()
    {
        _smouha = AddBranch("Smouha");
        _kafrAbdo = AddBranch("Kafr Abdo");
    }

    // ---- Branches ----

    [Fact]
    public async Task GetBranches_ManagersSeeOnlyTheirOwn_OwnerSeesAllSortedByName()
    {
        ActAs(RoleNames.BranchManager, _smouha);
        var managerView = await new GetBranchesQueryHandler(Context, CurrentUser).Handle(new GetBranchesQuery(), CancellationToken.None);

        ActAs(RoleNames.Owner);
        var ownerView = await new GetBranchesQueryHandler(Context, CurrentUser).Handle(new GetBranchesQuery(), CancellationToken.None);

        Assert.Equal("Smouha", Assert.Single(managerView).Name);
        Assert.Equal(["Kafr Abdo", "Smouha"], ownerView.Select(b => b.Name).ToArray());
    }

    [Fact]
    public async Task GetBranchById_OtherBranchForbidden_UnknownNotFound()
    {
        ActAs(RoleNames.BranchManager, _smouha);
        var handler = new GetBranchByIdQueryHandler(Context, CurrentUser);

        Assert.Equal("Smouha", (await handler.Handle(new GetBranchByIdQuery(_smouha.Id), CancellationToken.None)).Name);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new GetBranchByIdQuery(_kafrAbdo.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetBranchByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateBranch_ChangesDetailsAndCanDeactivate_UnknownNotFound()
    {
        var dto = await new UpdateBranchCommandHandler(Context)
            .Handle(new UpdateBranchCommand(_smouha.Id, "Smouha HQ", "New Address", "0345670000", false), CancellationToken.None);

        Assert.Equal("Smouha HQ", dto.Name);
        Assert.False(dto.IsActive);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateBranchCommandHandler(Context).Handle(new UpdateBranchCommand(Guid.NewGuid(), "x", "x", "x", true), CancellationToken.None));
    }

    [Fact]
    public async Task CreateBranch_StartsActive_AndIsPersisted()
    {
        var dto = await new CEMS.Application.Branches.Commands.CreateBranch.CreateBranchCommandHandler(Context)
            .Handle(new CEMS.Application.Branches.Commands.CreateBranch.CreateBranchCommand("Sidi Gaber", "5 Main St", "0345671111"), CancellationToken.None);

        Assert.True(dto.IsActive);
        Assert.Equal(3, Context.Branches.Count());
        Assert.Equal("Sidi Gaber", Context.Branches.Single(b => b.Id == dto.Id).Name);
    }

    [Fact]
    public async Task GetRoomById_ForAnotherBranchIsForbidden()
    {
        var room = AddRoom(_kafrAbdo);
        ActAs(RoleNames.FrontDesk, _smouha);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetRoomByIdQueryHandler(Context, CurrentUser).Handle(new GetRoomByIdQuery(room.Id), CancellationToken.None));
    }

    // ---- Rooms ----

    [Fact]
    public async Task CreateRoom_AtOwnBranchWorks_AtAnotherBranchForbidden_AtAnUnknownBranchNotFound()
    {
        ActAs(RoleNames.BranchManager, _smouha);
        var handler = new CreateRoomCommandHandler(Context, CurrentUser);

        var room = await handler.Handle(new CreateRoomCommand(_smouha.Id, "Lab 3", 12), CancellationToken.None);

        Assert.Equal(12, room.Capacity);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new CreateRoomCommand(_kafrAbdo.Id, "Lab", 5), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new CreateRoomCommand(Guid.NewGuid(), "Lab", 5), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateRoom_OwnBranchChangesIt_AnotherBranchIsForbidden()
    {
        var room = AddRoom(_smouha);

        ActAs(RoleNames.BranchManager, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new UpdateRoomCommandHandler(Context, CurrentUser).Handle(new UpdateRoomCommand(room.Id, "Hijacked", 1), CancellationToken.None));

        ActAs(RoleNames.BranchManager, _smouha);
        var dto = await new UpdateRoomCommandHandler(Context, CurrentUser).Handle(new UpdateRoomCommand(room.Id, "Lab 1B", 20), CancellationToken.None);
        Assert.Equal(("Lab 1B", 20), (dto.Name, dto.Capacity));
    }

    [Fact]
    public async Task RoomReads_AreBranchScoped()
    {
        AddRoom(_smouha, name: "Zeta");
        var alpha = AddRoom(_smouha, name: "Alpha");
        AddRoom(_kafrAbdo, name: "Other");
        ActAs(RoleNames.FrontDesk, _smouha);

        var list = await new GetRoomsByBranchQueryHandler(Context, CurrentUser).Handle(new GetRoomsByBranchQuery(_smouha.Id), CancellationToken.None);
        var single = await new GetRoomByIdQueryHandler(Context, CurrentUser).Handle(new GetRoomByIdQuery(alpha.Id), CancellationToken.None);

        Assert.Equal(["Alpha", "Zeta"], list.Select(r => r.Name).ToArray());
        Assert.Equal("Alpha", single.Name);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetRoomsByBranchQueryHandler(Context, CurrentUser).Handle(new GetRoomsByBranchQuery(_kafrAbdo.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetRoomByIdQueryHandler(Context, CurrentUser).Handle(new GetRoomByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    // ---- Curricula (org-wide catalog) ----

    [Fact]
    public async Task Curriculum_CreateUpdateAndRead_RoundTrips()
    {
        var created = await new CreateCurriculumCommandHandler(Context)
            .Handle(new CreateCurriculumCommand("Junior Coding", "Ages 8-14"), CancellationToken.None);
        await new CreateCurriculumCommandHandler(Context).Handle(new CreateCurriculumCommand("Advanced", "Teens"), CancellationToken.None);

        var updated = await new UpdateCurriculumCommandHandler(Context)
            .Handle(new UpdateCurriculumCommand(created.Id, "Junior Coding+", "Ages 8-15"), CancellationToken.None);
        var list = await new GetCurriculaQueryHandler(Context).Handle(new GetCurriculaQuery(), CancellationToken.None);
        var single = await new GetCurriculumByIdQueryHandler(Context).Handle(new GetCurriculumByIdQuery(created.Id), CancellationToken.None);

        Assert.Equal("Junior Coding+", updated.Name);
        Assert.Equal(2, list.Count);
        Assert.Equal("Ages 8-15", single.Description);
    }

    [Fact]
    public async Task Curriculum_UnknownIdIsNotFound_OnUpdateAndRead()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateCurriculumCommandHandler(Context).Handle(new UpdateCurriculumCommand(Guid.NewGuid(), "x", "y"), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCurriculumByIdQueryHandler(Context).Handle(new GetCurriculumByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    // ---- Courses ----

    [Fact]
    public async Task CreateCourse_AtOwnBranch_Works_ForeignBranchForbidden_UnknownReferencesNotFound()
    {
        var curriculum = AddCurriculum();
        ActAs(RoleNames.BranchManager, _smouha);
        var handler = new CreateCourseCommandHandler(Context, CurrentUser);

        var dto = await handler.Handle(new CreateCourseCommand("Python", DeliveryMode.Group, curriculum.Id, _smouha.Id), CancellationToken.None);

        Assert.Equal(_smouha.Id, dto.BranchId);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new CreateCourseCommand("X", DeliveryMode.Group, curriculum.Id, _kafrAbdo.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new CreateCourseCommand("X", DeliveryMode.Group, Guid.NewGuid(), _smouha.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new CreateCourseCommand("X", DeliveryMode.Group, curriculum.Id, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateCourse_ChangesNameAndMode_ButNotBranchOrCurriculum_AndIsBranchScoped()
    {
        var course = AddCourse(_smouha);

        ActAs(RoleNames.BranchManager, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new UpdateCourseCommandHandler(Context, CurrentUser).Handle(new UpdateCourseCommand(course.Id, "Hijacked", DeliveryMode.OneOnOne), CancellationToken.None));

        ActAs(RoleNames.BranchManager, _smouha);
        var dto = await new UpdateCourseCommandHandler(Context, CurrentUser)
            .Handle(new UpdateCourseCommand(course.Id, "Python 2", DeliveryMode.OneOnOne), CancellationToken.None);

        Assert.Equal(("Python 2", DeliveryMode.OneOnOne, _smouha.Id), (dto.Name, dto.DeliveryMode, dto.BranchId));
    }

    [Fact]
    public async Task CourseReads_AreBranchScoped()
    {
        var mine = AddCourse(_smouha, name: "B Mine");
        AddCourse(_smouha, name: "A Mine");
        var theirs = AddCourse(_kafrAbdo, name: "Theirs");
        ActAs(RoleNames.FrontDesk, _smouha);

        var list = await new GetCoursesQueryHandler(Context, CurrentUser).Handle(new GetCoursesQuery(), CancellationToken.None);
        var single = await new GetCourseByIdQueryHandler(Context, CurrentUser).Handle(new GetCourseByIdQuery(mine.Id), CancellationToken.None);

        Assert.Equal(["A Mine", "B Mine"], list.Select(c => c.Name).ToArray());
        Assert.Equal("B Mine", single.Name);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetCourseByIdQueryHandler(Context, CurrentUser).Handle(new GetCourseByIdQuery(theirs.Id), CancellationToken.None));

        ActAs(RoleNames.Owner);
        Assert.Equal(3, (await new GetCoursesQueryHandler(Context, CurrentUser).Handle(new GetCoursesQuery(), CancellationToken.None)).Count);
    }

    [Fact]
    public async Task GetMyCourses_ListsEachCourseTheCallerTeachesOnce_Alphabetically()
    {
        var teacher = AddTeacher(_smouha);
        var other = AddTeacher(_smouha);
        var room = AddRoom(_smouha);
        var python = AddCourse(_smouha, name: "Python");
        var scratch = AddCourse(_smouha, name: "Scratch");
        var notMine = AddCourse(_smouha, name: "Not Mine");
        AddSession(python, room, teacher, new DateTime(2030, 1, 7, 9, 0, 0, DateTimeKind.Utc));
        AddSession(python, room, teacher, new DateTime(2030, 1, 8, 9, 0, 0, DateTimeKind.Utc));
        AddSession(scratch, room, teacher, new DateTime(2030, 1, 9, 9, 0, 0, DateTimeKind.Utc));
        AddSession(notMine, room, other, new DateTime(2030, 1, 10, 9, 0, 0, DateTimeKind.Utc));
        CurrentUser.UserId = teacher.UserId;

        var result = await new GetMyCoursesQueryHandler(Context, CurrentUser).Handle(new GetMyCoursesQuery(), CancellationToken.None);

        Assert.Equal(["Python", "Scratch"], result.Select(c => c.Name).ToArray());
    }

    [Fact]
    public async Task CourseEnrollments_CanBeListedAndDropped_WithinTheBranchOnly()
    {
        var course = AddCourse(_smouha);
        var enrollment = Enroll(AddStudent(_smouha), course);

        ActAs(RoleNames.FrontDesk, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetEnrollmentsForCourseQueryHandler(Context, CurrentUser).Handle(new GetEnrollmentsForCourseQuery(course.Id), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new DropEnrollmentCommandHandler(Context, CurrentUser).Handle(new DropEnrollmentCommand(enrollment.Id), CancellationToken.None));

        ActAs(RoleNames.FrontDesk, _smouha);
        Assert.Single(await new GetEnrollmentsForCourseQueryHandler(Context, CurrentUser).Handle(new GetEnrollmentsForCourseQuery(course.Id), CancellationToken.None));
        await new DropEnrollmentCommandHandler(Context, CurrentUser).Handle(new DropEnrollmentCommand(enrollment.Id), CancellationToken.None);

        Assert.Equal(CourseEnrollmentStatus.Dropped, Context.CourseEnrollments.Single().Status);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DropEnrollmentCommandHandler(Context, CurrentUser).Handle(new DropEnrollmentCommand(Guid.NewGuid()), CancellationToken.None));
    }

    // ---- Packages ----

    [Fact]
    public async Task CreatePackage_AtOwnBranchWorks_ForeignBranchForbidden_UnknownCourseNotFound()
    {
        var course = AddCourse(_smouha);
        ActAs(RoleNames.BranchManager, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new CreatePackageCommandHandler(Context, CurrentUser).Handle(new CreatePackageCommand(course.Id, 12, 2400), CancellationToken.None));

        ActAs(RoleNames.BranchManager, _smouha);
        var dto = await new CreatePackageCommandHandler(Context, CurrentUser).Handle(new CreatePackageCommand(course.Id, 12, 2400), CancellationToken.None);
        Assert.Equal(2400, dto.Price);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CreatePackageCommandHandler(Context, CurrentUser).Handle(new CreatePackageCommand(Guid.NewGuid(), 12, 2400), CancellationToken.None));
    }

    [Fact]
    public async Task UpdatePackage_ChangesThePrice_ButAlreadyIssuedInvoicesKeepTheirOriginalAmount()
    {
        var course = AddCourse(_smouha);
        var package = AddPackage(course, price: 2400);
        var student = AddStudent(_smouha);
        ActAs(RoleNames.Owner);
        var invoice = await new CreateInvoiceCommandHandler(Context, CurrentUser)
            .Handle(new CreateInvoiceCommand(student.Id, package.Id, null, new DateOnly(2030, 1, 1)), CancellationToken.None);

        var updated = await new UpdatePackageCommandHandler(Context, CurrentUser).Handle(new UpdatePackageCommand(package.Id, 12, 3000), CancellationToken.None);

        Assert.Equal(3000, updated.Price);
        Assert.Equal(2400, Context.Invoices.Single(i => i.Id == invoice.Id).Amount);
    }

    [Fact]
    public async Task PackageReads_AreBranchScoped_AndSortedBySessionCount()
    {
        var course = AddCourse(_smouha);
        AddPackage(course, 5000, 24);
        var small = AddPackage(course, 2400, 12);
        ActAs(RoleNames.FrontDesk, _smouha);

        var list = await new GetPackagesForCourseQueryHandler(Context, CurrentUser).Handle(new GetPackagesForCourseQuery(course.Id), CancellationToken.None);
        var single = await new GetPackageByIdQueryHandler(Context, CurrentUser).Handle(new GetPackageByIdQuery(small.Id), CancellationToken.None);

        Assert.Equal([12, 24], list.Select(p => p.SessionCount).ToArray());
        Assert.Equal(2400, single.Price);

        ActAs(RoleNames.FrontDesk, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetPackagesForCourseQueryHandler(Context, CurrentUser).Handle(new GetPackagesForCourseQuery(course.Id), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetPackageByIdQueryHandler(Context, CurrentUser).Handle(new GetPackageByIdQuery(small.Id), CancellationToken.None));
    }
}

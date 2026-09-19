using CEMS.Application.Common.Exceptions;
using CEMS.Application.Courses;
using CEMS.Application.Courses.Commands.EnrollStudent;
using CEMS.Application.Courses.Commands.PromoteFromWaitlist;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Students;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Courses;

public class EnrollmentCommandHandlerTests : HandlerTestBase
{
    private Branch _branch = null!;
    private Room _room = null!;
    private Course _course = null!;
    private Teacher _teacher = null!;

    private void Seed(int roomCapacity, bool withSession = true)
    {
        _branch = new Branch { Id = Guid.NewGuid(), Name = "Smouha" };
        _room = new Room { Id = Guid.NewGuid(), Name = "Lab 1", Capacity = roomCapacity, BranchId = _branch.Id };
        var curriculum = new Curriculum { Id = Guid.NewGuid(), Name = "Web", Description = "Web" };
        _course = new Course { Id = Guid.NewGuid(), Name = "Python", DeliveryMode = DeliveryMode.Group, CurriculumId = curriculum.Id, BranchId = _branch.Id };
        _teacher = new Teacher { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), HireDate = new DateOnly(2024, 1, 1), PayType = PayType.Hourly, PayRate = 100 };

        Context.AddRange(_branch, _room, curriculum, _course, _teacher);

        if (withSession)
        {
            AddSession(SessionStatus.Scheduled, _room, DateTime.UtcNow.AddDays(3));
        }

        Context.SaveChanges();

        CurrentUser.UserId = Guid.NewGuid();
        CurrentUser.Roles = [RoleNames.FrontDesk];
        CurrentUser.BranchIds = [_branch.Id];
    }

    private void AddSession(SessionStatus status, Room room, DateTime start)
    {
        Context.CourseSessions.Add(new CourseSession
        {
            Id = Guid.NewGuid(), CourseId = _course.Id, RoomId = room.Id, TeacherId = _teacher.Id,
            StartUtc = start, EndUtc = start.AddHours(1), Status = status
        });
    }

    private Student AddStudent(Guid? branchId = null)
    {
        var student = new Student
        {
            Id = Guid.NewGuid(), FullName = $"Student {Guid.NewGuid():N}", DateOfBirth = new DateOnly(2012, 1, 1),
            Gender = Gender.Female, EnrollmentDate = new DateOnly(2024, 1, 1), CurrentBranchId = branchId ?? _branch.Id
        };
        Context.Students.Add(student);
        Context.SaveChanges();
        return student;
    }

    private EnrollStudentCommandHandler EnrollHandler() => new(Context, CurrentUser);

    private Task<CourseEnrollmentDto> Enroll(Student student) =>
        EnrollHandler().Handle(new EnrollStudentCommand(student.Id, _course.Id), CancellationToken.None);

    [Fact]
    public async Task Enroll_CourseWithSpace_IsActiveWithNoWaitlistPosition()
    {
        Seed(roomCapacity: 2);

        var result = await Enroll(AddStudent());

        Assert.Equal(CourseEnrollmentStatus.Active, result.Status);
        Assert.Null(result.Position);
    }

    [Fact]
    public async Task Enroll_CourseWithNoSessions_HasNoKnownCapacityAndStaysActive()
    {
        Seed(roomCapacity: 1, withSession: false);

        var first = await Enroll(AddStudent());
        var second = await Enroll(AddStudent());

        Assert.Equal(CourseEnrollmentStatus.Active, first.Status);
        Assert.Equal(CourseEnrollmentStatus.Active, second.Status);
    }

    [Fact]
    public async Task Enroll_FullCourse_WaitlistsWithIncreasingPositions()
    {
        Seed(roomCapacity: 1);

        var active = await Enroll(AddStudent());
        var waitlistedFirst = await Enroll(AddStudent());
        var waitlistedSecond = await Enroll(AddStudent());

        Assert.Equal(CourseEnrollmentStatus.Active, active.Status);
        Assert.Equal(CourseEnrollmentStatus.Waitlisted, waitlistedFirst.Status);
        Assert.Equal(1, waitlistedFirst.Position);
        Assert.Equal(CourseEnrollmentStatus.Waitlisted, waitlistedSecond.Status);
        Assert.Equal(2, waitlistedSecond.Position);
    }

    [Fact]
    public async Task Enroll_CapacityComesFromEarliestNonCancelledSession()
    {
        Seed(roomCapacity: 1, withSession: false);
        var bigRoom = new Room { Id = Guid.NewGuid(), Name = "Big", Capacity = 30, BranchId = _branch.Id };
        Context.Rooms.Add(bigRoom);
        AddSession(SessionStatus.Cancelled, _room, DateTime.UtcNow.AddDays(1));
        AddSession(SessionStatus.Scheduled, bigRoom, DateTime.UtcNow.AddDays(2));
        Context.SaveChanges();

        var first = await Enroll(AddStudent());
        var second = await Enroll(AddStudent());

        Assert.Equal(CourseEnrollmentStatus.Active, first.Status);
        Assert.Equal(CourseEnrollmentStatus.Active, second.Status);
    }

    [Fact]
    public async Task Enroll_StudentAlreadyEnrolledOrWaitlisted_ThrowsBadRequest()
    {
        Seed(roomCapacity: 1);
        var student = AddStudent();
        await Enroll(student);

        await Assert.ThrowsAsync<BadRequestException>(() => Enroll(student));
    }

    [Fact]
    public async Task Enroll_StudentWhoDroppedEarlier_CanEnrollAgain()
    {
        Seed(roomCapacity: 5);
        var student = AddStudent();
        var first = await Enroll(student);
        Context.CourseEnrollments.Single(e => e.Id == first.Id).Status = CourseEnrollmentStatus.Dropped;
        Context.SaveChanges();

        var again = await Enroll(student);

        Assert.Equal(CourseEnrollmentStatus.Active, again.Status);
    }

    [Fact]
    public async Task Enroll_StudentFromDifferentBranch_ThrowsBadRequest()
    {
        Seed(roomCapacity: 5);
        var otherBranch = new Branch { Id = Guid.NewGuid(), Name = "Kafr Abdo" };
        Context.Branches.Add(otherBranch);
        Context.SaveChanges();

        await Assert.ThrowsAsync<BadRequestException>(() => Enroll(AddStudent(otherBranch.Id)));
    }

    [Fact]
    public async Task Enroll_UserWithoutAccessToTheCourseBranch_ThrowsForbidden()
    {
        Seed(roomCapacity: 5);
        var student = AddStudent();
        CurrentUser.BranchIds = [Guid.NewGuid()];

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Enroll(student));
    }

    [Fact]
    public async Task Promote_WaitlistedEnrollment_BecomesActiveAndLosesPosition()
    {
        Seed(roomCapacity: 1);
        await Enroll(AddStudent());
        var waitlisted = await Enroll(AddStudent());

        var promoted = await new PromoteFromWaitlistCommandHandler(Context, CurrentUser)
            .Handle(new PromoteFromWaitlistCommand(waitlisted.Id), CancellationToken.None);

        Assert.Equal(CourseEnrollmentStatus.Active, promoted.Status);
        Assert.Null(promoted.Position);
    }

    [Fact]
    public async Task Promote_ActiveEnrollment_ThrowsBadRequest()
    {
        Seed(roomCapacity: 5);
        var active = await Enroll(AddStudent());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            new PromoteFromWaitlistCommandHandler(Context, CurrentUser)
                .Handle(new PromoteFromWaitlistCommand(active.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Promote_UserWithoutAccessToTheCourseBranch_ThrowsForbidden()
    {
        Seed(roomCapacity: 1);
        await Enroll(AddStudent());
        var waitlisted = await Enroll(AddStudent());
        CurrentUser.BranchIds = [Guid.NewGuid()];

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new PromoteFromWaitlistCommandHandler(Context, CurrentUser)
                .Handle(new PromoteFromWaitlistCommand(waitlisted.Id), CancellationToken.None));
    }
}

using CEMS.Application.Common.Exceptions;
using CEMS.Application.Payroll.Commands.GeneratePayrollRun;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Payments;
using CEMS.Domain.Students;
using CEMS.Domain.Teachers;

namespace CEMS.Application.Tests.Payroll;

public class GeneratePayrollRunCommandHandlerTests : HandlerTestBase
{
    private Guid _branchId;
    private Guid _courseId;

    private Guid SeedCourseAndBranch()
    {
        var branch = new Branch { Id = Guid.NewGuid(), Name = "Downtown" };
        var room = new Room { Id = Guid.NewGuid(), Name = "Room A", Capacity = 10, BranchId = branch.Id };
        var curriculum = new Curriculum { Id = Guid.NewGuid(), Name = "IG", Description = "IG" };
        var course = new Course { Id = Guid.NewGuid(), Name = "Math", DeliveryMode = DeliveryMode.Group, CurriculumId = curriculum.Id, BranchId = branch.Id };

        Context.AddRange(branch, room, curriculum, course);
        Context.SaveChanges();

        _branchId = branch.Id;
        _courseId = course.Id;
        return room.Id;
    }

    private Teacher SeedTeacher(PayType payType, decimal payRate)
    {
        var teacher = new Teacher { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), HireDate = DateOnly.FromDateTime(DateTime.UtcNow), PayType = payType, PayRate = payRate };
        Context.Teachers.Add(teacher);
        Context.SaveChanges();
        return teacher;
    }

    private void SeedSession(Guid teacherId, Guid roomId, DateTime startUtc, DateTime endUtc, SessionStatus status = SessionStatus.Scheduled)
    {
        Context.CourseSessions.Add(new CourseSession
        {
            Id = Guid.NewGuid(),
            CourseId = _courseId,
            RoomId = roomId,
            TeacherId = teacherId,
            StartUtc = startUtc,
            EndUtc = endUtc,
            Status = status
        });
        Context.SaveChanges();
    }

    private GeneratePayrollRunCommandHandler CreateHandler() => new(Context);

    [Fact]
    public async Task Handle_HourlyTeacher_PaysRateTimesSessionDuration()
    {
        var roomId = SeedCourseAndBranch();
        var teacher = SeedTeacher(PayType.Hourly, 100);
        SeedSession(teacher.Id, roomId, new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc)); // 2 hours

        var handler = CreateHandler();
        var result = await handler.Handle(new GeneratePayrollRunCommand(teacher.Id, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)), CancellationToken.None);

        Assert.Equal(200m, result.TotalAmount);
    }

    [Fact]
    public async Task Handle_PerSessionTeacher_PaysFlatRatePerSession()
    {
        var roomId = SeedCourseAndBranch();
        var teacher = SeedTeacher(PayType.PerSession, 50);
        SeedSession(teacher.Id, roomId, new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));
        SeedSession(teacher.Id, roomId, new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 8, 11, 0, 0, DateTimeKind.Utc));

        var handler = CreateHandler();
        var result = await handler.Handle(new GeneratePayrollRunCommand(teacher.Id, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)), CancellationToken.None);

        Assert.Equal(100m, result.TotalAmount); // 2 sessions x 50, regardless of duration
    }

    [Fact]
    public async Task Handle_CancelledSession_IsNotPaid()
    {
        var roomId = SeedCourseAndBranch();
        var teacher = SeedTeacher(PayType.PerSession, 50);
        SeedSession(teacher.Id, roomId, new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 1, 11, 0, 0, DateTimeKind.Utc), SessionStatus.Cancelled);

        var handler = CreateHandler();
        var result = await handler.Handle(new GeneratePayrollRunCommand(teacher.Id, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)), CancellationToken.None);

        Assert.Equal(0m, result.TotalAmount);
    }

    [Fact]
    public async Task Handle_FixedTeacher_PaysFlatAmountRegardlessOfSessions()
    {
        var roomId = SeedCourseAndBranch();
        var teacher = SeedTeacher(PayType.Fixed, 3000);
        SeedSession(teacher.Id, roomId, new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 1, 11, 0, 0, DateTimeKind.Utc));

        var handler = CreateHandler();
        var result = await handler.Handle(new GeneratePayrollRunCommand(teacher.Id, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)), CancellationToken.None);

        Assert.Equal(3000m, result.TotalAmount);
    }

    [Fact]
    public async Task Handle_FixedTeacher_PaysFlatAmountEvenWithNoSessions()
    {
        SeedCourseAndBranch();
        var teacher = SeedTeacher(PayType.Fixed, 3000);

        var handler = CreateHandler();
        var result = await handler.Handle(new GeneratePayrollRunCommand(teacher.Id, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)), CancellationToken.None);

        Assert.Equal(3000m, result.TotalAmount);
    }

    [Fact]
    public async Task Handle_PercentageTeacher_PaysPercentageOfPaymentsCollectedInPeriod()
    {
        var roomId = SeedCourseAndBranch();
        var teacher = SeedTeacher(PayType.Percentage, 20);
        SeedSession(teacher.Id, roomId, new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 1, 11, 0, 0, DateTimeKind.Utc));

        var student = new Student { Id = Guid.NewGuid(), FullName = "Sam Student", DateOfBirth = new DateOnly(2013, 1, 1), Gender = Gender.Male, EnrollmentDate = DateOnly.FromDateTime(DateTime.UtcNow), CurrentBranchId = _branchId };
        var package = new Package { Id = Guid.NewGuid(), CourseId = _courseId, SessionCount = 10, Price = 500 };
        var invoice = new Invoice { Id = Guid.NewGuid(), StudentId = student.Id, PackageId = package.Id, Amount = 500, IssuedDate = new DateOnly(2026, 6, 1), DueDate = new DateOnly(2026, 6, 15) };
        var payment = new Payment { Id = Guid.NewGuid(), InvoiceId = invoice.Id, AmountPaid = 500, PaymentDate = new DateOnly(2026, 6, 10), Method = PaymentMethod.Card, ReceivedByUserId = Guid.NewGuid() };

        Context.AddRange(student, package, invoice, payment);
        Context.SaveChanges();

        var handler = CreateHandler();
        var result = await handler.Handle(new GeneratePayrollRunCommand(teacher.Id, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)), CancellationToken.None);

        Assert.Equal(100m, result.TotalAmount); // 20% of 500
    }

    [Fact]
    public async Task Handle_PercentageTeacher_IgnoresPaymentsOutsidePeriod()
    {
        var roomId = SeedCourseAndBranch();
        var teacher = SeedTeacher(PayType.Percentage, 20);
        SeedSession(teacher.Id, roomId, new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 1, 11, 0, 0, DateTimeKind.Utc));

        var student = new Student { Id = Guid.NewGuid(), FullName = "Sam Student", DateOfBirth = new DateOnly(2013, 1, 1), Gender = Gender.Male, EnrollmentDate = DateOnly.FromDateTime(DateTime.UtcNow), CurrentBranchId = _branchId };
        var package = new Package { Id = Guid.NewGuid(), CourseId = _courseId, SessionCount = 10, Price = 500 };
        var invoice = new Invoice { Id = Guid.NewGuid(), StudentId = student.Id, PackageId = package.Id, Amount = 500, IssuedDate = new DateOnly(2026, 5, 1), DueDate = new DateOnly(2026, 5, 15) };
        var payment = new Payment { Id = Guid.NewGuid(), InvoiceId = invoice.Id, AmountPaid = 500, PaymentDate = new DateOnly(2026, 5, 10), Method = PaymentMethod.Card, ReceivedByUserId = Guid.NewGuid() }; // paid in May, run is for June

        Context.AddRange(student, package, invoice, payment);
        Context.SaveChanges();

        var handler = CreateHandler();
        var result = await handler.Handle(new GeneratePayrollRunCommand(teacher.Id, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)), CancellationToken.None);

        Assert.Equal(0m, result.TotalAmount);
    }

    [Fact]
    public async Task Handle_OverlappingPeriodForSameTeacher_ThrowsBadRequest()
    {
        SeedCourseAndBranch();
        var teacher = SeedTeacher(PayType.Fixed, 1000);
        var handler = CreateHandler();

        await handler.Handle(new GeneratePayrollRunCommand(teacher.Id, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)), CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Handle(new GeneratePayrollRunCommand(teacher.Id, new DateOnly(2026, 6, 15), new DateOnly(2026, 7, 15)), CancellationToken.None));
    }
}

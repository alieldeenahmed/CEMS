using CEMS.Application.Common.Exceptions;
using CEMS.Application.Courses.Commands.EnrollStudent;
using CEMS.Application.Payments.Commands.CancelInvoice;
using CEMS.Application.Payments.Commands.RecordPayment;
using CEMS.Application.Payroll.Commands.ApprovePayrollRun;
using CEMS.Application.Payroll.Commands.GeneratePayrollRun;
using CEMS.Application.Payroll.Commands.GenerateStaffPayrollRun;
using CEMS.Application.Payroll.Commands.UpdateStaffPayrollRun;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Payments;
using CEMS.Domain.Payroll;
using CEMS.Domain.Students;
using CEMS.Domain.Teachers;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Postgres.Tests;

/// <summary>
/// The other check-then-write sequences in the system -- enrolling, paying, cancelling, generating and
/// approving payroll -- with genuinely simultaneous requests against PostgreSQL.
/// </summary>
public class BusinessInvariantConcurrencyTests : IAsyncLifetime
{
    private PostgresTestDatabase _db = null!;
    private Guid _branchId;
    private Guid _courseId;
    private Guid _roomId;
    private Guid _teacherId;
    private readonly List<Guid> _studentIds = new();

    public async Task InitializeAsync()
    {
        _db = await PostgresTestDatabase.CreateAsync();
        await using var context = _db.CreateContext();

        var branch = new Branch { Id = Guid.NewGuid(), Name = "Smouha", Address = "14 Fawzy Moaz St", Phone = "034567001" };
        var curriculum = new Curriculum { Id = Guid.NewGuid(), Name = "Web", Description = "Web" };
        var course = new Course { Id = Guid.NewGuid(), Name = "Python", DeliveryMode = DeliveryMode.Group, CurriculumId = curriculum.Id, BranchId = branch.Id };
        var room = new Room { Id = Guid.NewGuid(), Name = "Booth", Capacity = 1, BranchId = branch.Id };
        var teacher = new Teacher { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), HireDate = new DateOnly(2024, 1, 1), PayType = PayType.Fixed, PayRate = 9000 };
        context.AddRange(branch, curriculum, course, room, teacher);
        context.TeacherBranches.Add(new TeacherBranch { TeacherId = teacher.Id, BranchId = branch.Id });

        for (var i = 0; i < 6; i++)
        {
            var student = new Student
            {
                Id = Guid.NewGuid(), FullName = $"Student {i}", DateOfBirth = new DateOnly(2013, 4, 12), Gender = Gender.Male,
                EnrollmentDate = new DateOnly(2024, 1, 1), CurrentBranchId = branch.Id
            };
            context.Students.Add(student);
            _studentIds.Add(student.Id);
        }

        // Capacity comes from the room of the course's earliest session, so give the course one (capacity 1).
        context.CourseSessions.Add(new CourseSession
        {
            Id = Guid.NewGuid(), CourseId = course.Id, RoomId = room.Id, TeacherId = teacher.Id,
            StartUtc = new DateTime(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc), EndUtc = new DateTime(2030, 1, 7, 11, 0, 0, DateTimeKind.Utc)
        });

        await context.SaveChangesAsync();
        (_branchId, _courseId, _roomId, _teacherId) = (branch.Id, course.Id, room.Id, teacher.Id);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private FakeUser Owner => new();

    private async Task<T> Run<T>(Func<CEMS.Infrastructure.Persistence.ApplicationDbContext, Task<T>> action)
    {
        await using var context = _db.CreateContext();
        return await action(context);
    }

    private async Task<Guid> AddInvoiceAsync(decimal amount)
    {
        await using var context = _db.CreateContext();
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), StudentId = _studentIds[0], Amount = amount, Status = InvoiceStatus.Pending,
            IssuedDate = new DateOnly(2025, 1, 1), DueDate = new DateOnly(2030, 1, 1)
        };
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();
        return invoice.Id;
    }

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    // ---- Enrollment ----

    [Fact]
    public async Task SixSimultaneousEnrollmentsIntoAOneSeatCourse_FillOneSeat_AndQueueTheRestInOrder()
    {
        var outcomes = await SchedulingWorld.RaceAsync(6, i => Run(c =>
            new EnrollStudentCommandHandler(c, Owner).Handle(new EnrollStudentCommand(_studentIds[i], _courseId), CancellationToken.None)));

        Assert.All(outcomes, o => Assert.True(o.Succeeded, o.Error?.ToString()));
        var enrollments = await Run(c => c.CourseEnrollments.AsNoTracking().ToListAsync());
        Assert.Equal(1, enrollments.Count(e => e.Status == CourseEnrollmentStatus.Active));
        var positions = enrollments.Where(e => e.Status == CourseEnrollmentStatus.Waitlisted).Select(e => e.Position!.Value).OrderBy(p => p).ToList();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, positions);   // no duplicates, no gaps
    }

    [Fact]
    public async Task TheSameStudentEnrollingFiveTimesAtOnce_IsEnrolledExactlyOnce()
    {
        var outcomes = await SchedulingWorld.RaceAsync(5, _ => Run(c =>
            new EnrollStudentCommandHandler(c, Owner).Handle(new EnrollStudentCommand(_studentIds[0], _courseId), CancellationToken.None)));

        Assert.Equal(1, outcomes.Count(o => o.Succeeded));
        Assert.All(outcomes.Where(o => !o.Succeeded), o => Assert.IsType<BadRequestException>(o.Error));
        Assert.Single(await Run(c => c.CourseEnrollments.AsNoTracking().ToListAsync()));
    }

    [Fact]
    public async Task TheDatabaseItselfRefusesASecondLiveEnrollment_ButAllowsReEnrollingAfterADrop()
    {
        await using var context = _db.CreateContext();
        var student = _studentIds[0];
        context.CourseEnrollments.Add(NewEnrollment(student, CourseEnrollmentStatus.Dropped));
        context.CourseEnrollments.Add(NewEnrollment(student, CourseEnrollmentStatus.Active));
        await context.SaveChangesAsync();   // a dropped one plus a live one is how re-enrolling looks

        await using var second = _db.CreateContext();
        second.CourseEnrollments.Add(NewEnrollment(student, CourseEnrollmentStatus.Waitlisted, position: 1));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => second.SaveChangesAsync());
        Assert.Contains("already enrolled", ex.Errors.Single());
    }

    private CourseEnrollment NewEnrollment(Guid studentId, CourseEnrollmentStatus status, int? position = null) => new()
    {
        Id = Guid.NewGuid(), StudentId = studentId, CourseId = _courseId, EnrollmentDate = Today, Status = status, Position = position
    };

    // ---- Payments ----

    [Fact]
    public async Task TwoSimultaneous600PaymentsOnA1000Invoice_ExactlyOneIsAccepted()
    {
        var invoiceId = await AddInvoiceAsync(1000);

        var outcomes = await SchedulingWorld.RaceAsync(2, _ => Run(c =>
            new RecordPaymentCommandHandler(c, Owner).Handle(new RecordPaymentCommand(invoiceId, 600, Today, PaymentMethod.Cash), CancellationToken.None)));

        Assert.Equal(1, outcomes.Count(o => o.Succeeded));
        Assert.All(outcomes.Where(o => !o.Succeeded), o => Assert.IsType<BadRequestException>(o.Error));
        var invoice = await Run(c => c.Invoices.AsNoTracking().Include(i => i.Payments).SingleAsync(i => i.Id == invoiceId));
        Assert.Equal(600m, invoice.Payments.Sum(p => p.AmountPaid));
        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
    }

    [Fact]
    public async Task TwoSimultaneousHalfPayments_BothLand_AndTheInvoiceEndsPaid()
    {
        var invoiceId = await AddInvoiceAsync(1000);

        var outcomes = await SchedulingWorld.RaceAsync(2, _ => Run(c =>
            new RecordPaymentCommandHandler(c, Owner).Handle(new RecordPaymentCommand(invoiceId, 500, Today, PaymentMethod.Cash), CancellationToken.None)));

        // Unlocked, each request would see "0 paid so far", compute PartiallyPaid, and the invoice would end up
        // fully paid but marked partial.
        Assert.All(outcomes, o => Assert.True(o.Succeeded, o.Error?.ToString()));
        var invoice = await Run(c => c.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId));
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public async Task ACancellationRacingAPayment_NeverLeavesACancelledInvoiceWithMoneyOnIt()
    {
        var invoiceId = await AddInvoiceAsync(1000);

        var outcomes = await SchedulingWorld.RaceAsync(2, async i =>
        {
            if (i == 0)
            {
                return await Run(async c =>
                {
                    await new RecordPaymentCommandHandler(c, Owner).Handle(new RecordPaymentCommand(invoiceId, 300, Today, PaymentMethod.Cash), CancellationToken.None);
                    return "paid";
                });
            }

            return await Run(async c =>
            {
                await new CancelInvoiceCommandHandler(c, Owner).Handle(new CancelInvoiceCommand(invoiceId), CancellationToken.None);
                return "cancelled";
            });
        });

        // Either order is legal, but a payment must never be accepted *after* the cancellation, and a
        // cancellation of a partly paid invoice keeps its payment on record (it is not silently dropped).
        var invoice = await Run(c => c.Invoices.AsNoTracking().Include(i => i.Payments).SingleAsync(i => i.Id == invoiceId));
        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            var paymentSucceeded = outcomes[0].Succeeded;
            Assert.Equal(paymentSucceeded ? 1 : 0, invoice.Payments.Count);
            if (!paymentSucceeded)
            {
                Assert.IsType<BadRequestException>(outcomes[0].Error);   // "cannot record a payment against a cancelled invoice"
            }
        }
        else
        {
            Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
            Assert.False(outcomes[1].Succeeded);
        }
    }

    [Fact]
    public async Task DecimalCentsAreExact_InThePaymentTotalsAndTheStoredStatus()
    {
        var invoiceId = await AddInvoiceAsync(0.30m);
        await Run(c => new RecordPaymentCommandHandler(c, Owner).Handle(new RecordPaymentCommand(invoiceId, 0.10m, Today, PaymentMethod.Cash), CancellationToken.None));
        await Run(c => new RecordPaymentCommandHandler(c, Owner).Handle(new RecordPaymentCommand(invoiceId, 0.20m, Today, PaymentMethod.Cash), CancellationToken.None));

        var invoice = await Run(c => c.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId));

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);   // 0.10 + 0.20 == 0.30 exactly, as numeric, not as a float
        Assert.Equal(0.30m, await Run(c => c.Payments.Where(p => p.InvoiceId == invoiceId).SumAsync(p => p.AmountPaid)));
    }

    // ---- Payroll ----

    [Fact]
    public async Task FiveSimultaneousPayrollRunsForTheSameTeacherAndPeriod_CreateExactlyOne()
    {
        var outcomes = await SchedulingWorld.RaceAsync(5, _ => Run(c =>
            new GeneratePayrollRunCommandHandler(c).Handle(
                new GeneratePayrollRunCommand(_teacherId, new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 31)), CancellationToken.None)));

        Assert.Equal(1, outcomes.Count(o => o.Succeeded));
        Assert.All(outcomes.Where(o => !o.Succeeded), o => Assert.IsType<BadRequestException>(o.Error));
        Assert.Single(await Run(c => c.PayrollRuns.AsNoTracking().ToListAsync()));
    }

    [Fact]
    public async Task OverlappingPayrollPeriods_AreRefusedByTheDatabaseToo_ButAdjacentOnesAreFine()
    {
        await using var context = _db.CreateContext();
        context.PayrollRuns.Add(NewRun(new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 31)));
        context.PayrollRuns.Add(NewRun(new DateOnly(2030, 2, 1), new DateOnly(2030, 2, 28)));   // adjacent: fine
        await context.SaveChangesAsync();

        await using var overlapping = _db.CreateContext();
        overlapping.PayrollRuns.Add(NewRun(new DateOnly(2030, 1, 31), new DateOnly(2030, 2, 1)));   // shares 31 Jan (inclusive periods)

        await Assert.ThrowsAsync<BadRequestException>(() => overlapping.SaveChangesAsync());
    }

    private PayrollRun NewRun(DateOnly start, DateOnly end) => new()
    {
        Id = Guid.NewGuid(), TeacherId = _teacherId, PeriodStart = start, PeriodEnd = end, TotalAmount = 100, Status = PayrollRunStatus.Draft
    };

    [Fact]
    public async Task AnAmountEditRacingAnApproval_CannotChangeAnApprovedRun()
    {
        Guid runId;
        await using (var context = _db.CreateContext())
        {
            var run = new StaffPayrollRun
            {
                Id = Guid.NewGuid(), UserId = Guid.NewGuid(), PeriodStart = new DateOnly(2030, 1, 1), PeriodEnd = new DateOnly(2030, 1, 31),
                Amount = 5000, Status = PayrollRunStatus.Draft
            };
            context.StaffPayrollRuns.Add(run);
            await context.SaveChangesAsync();
            runId = run.Id;
        }

        var outcomes = await SchedulingWorld.RaceAsync(2, i => i == 0
            ? Run(async c => { await new CEMS.Application.Payroll.Commands.ApproveStaffPayrollRun.ApproveStaffPayrollRunCommandHandler(c).Handle(new CEMS.Application.Payroll.Commands.ApproveStaffPayrollRun.ApproveStaffPayrollRunCommand(runId), CancellationToken.None); return "approved"; })
            : Run(async c => { await new UpdateStaffPayrollRunCommandHandler(c).Handle(new UpdateStaffPayrollRunCommand(runId, 9999), CancellationToken.None); return "edited"; }));

        var stored = await Run(c => c.StaffPayrollRuns.AsNoTracking().SingleAsync(r => r.Id == runId));
        Assert.Equal(PayrollRunStatus.Approved, stored.Status);
        if (outcomes[1].Succeeded)
        {
            Assert.Equal(9999m, stored.Amount);   // the edit landed first, then the approval
        }
        else
        {
            Assert.Equal(5000m, stored.Amount);   // the approval landed first, so the edit was refused
            Assert.IsType<BadRequestException>(outcomes[1].Error);
        }
    }

    // ---- Row-level checks ----

    [Fact]
    public async Task TheDatabaseRejectsRowsThatBreakBasicMoneyRules()
    {
        var invoiceId = await AddInvoiceAsync(100);

        await using var context = _db.CreateContext();
        context.Payments.Add(new Payment { Id = Guid.NewGuid(), InvoiceId = invoiceId, AmountPaid = -5, PaymentDate = Today, Method = PaymentMethod.Cash, ReceivedByUserId = Guid.NewGuid() });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("ck_payments_amount_positive", ex.InnerException?.Message);
    }
}

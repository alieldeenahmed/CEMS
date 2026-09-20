using CEMS.Application.Common.Exceptions;
using CEMS.Application.Courses.Commands.DropEnrollment;
using CEMS.Application.Exams.Commands.RecordGrade;
using CEMS.Application.Payments.Commands.CreateInvoice;
using CEMS.Application.Payments.Commands.CreatePackage;
using CEMS.Application.Payments.Commands.RecordPayment;
using CEMS.Application.Payroll.Commands.GeneratePayrollRun;
using CEMS.Application.Students.Commands.TransferStudentBranch;
using CEMS.Application.Teachers.Commands.CreateTeacher;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Payments;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;
using FluentValidation;

namespace CEMS.Application.Tests.Common;

/// <summary>
/// Business invariants that used to be enforced nowhere (or only by the UI): each test here would have
/// passed a bad request straight through before.
/// </summary>
public class BusinessRuleRegressionTests : PipelineTestBase
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly Branch _smouha;
    private readonly Branch _kafrAbdo;
    private readonly Course _course;

    public BusinessRuleRegressionTests()
    {
        _smouha = AddBranch("Smouha");
        _kafrAbdo = AddBranch("Kafr Abdo");
        _course = AddCourse(_smouha);
        ActAs(RoleNames.Owner);
    }

    // ---- Money and scores: what is validated is what the column can hold ----

    [Theory]
    [InlineData(100.005)]      // a third decimal place the column would silently round
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100000000)]    // more digits than numeric(10,2) holds: would be a database error, not a 400
    public async Task RecordPayment_RejectsAmountsTheColumnCannotStoreFaithfully(double amount)
    {
        var invoice = AddInvoice(AddStudent(_smouha), 500);

        await Assert.ThrowsAsync<ValidationException>(() =>
            Mediator.Send(new RecordPaymentCommand(invoice.Id, (decimal)amount, Today, PaymentMethod.Cash)));
    }

    [Fact]
    public async Task RecordPayment_AcceptsWholeAndCentAmounts()
    {
        var invoice = AddInvoice(AddStudent(_smouha), 500);

        await Mediator.Send(new RecordPaymentCommand(invoice.Id, 199.99m, Today, PaymentMethod.Cash));
        await Mediator.Send(new RecordPaymentCommand(invoice.Id, 300.01m, Today, PaymentMethod.Cash));

        Assert.Equal(InvoiceStatus.Paid, Context.Invoices.Single().Status);
    }

    [Fact]
    public async Task OtherMoneyInputs_ShareTheSameRule()
    {
        var student = AddStudent(_smouha);

        await Assert.ThrowsAsync<ValidationException>(() => Mediator.Send(new CreateInvoiceCommand(student.Id, null, 10.999m, Today)));
        await Assert.ThrowsAsync<ValidationException>(() => Mediator.Send(new CreatePackageCommand(_course.Id, 12, 2400.555m)));

        var user = Guid.NewGuid();
        await Assert.ThrowsAsync<ValidationException>(() => Mediator.Send(new CreateTeacherCommand(user, new DateOnly(2024, 1, 1), PayType.Hourly, 150.123m)));
    }

    [Fact]
    public async Task RecordGrade_ScoresAreValidatedAgainstTheirColumnToo()
    {
        var exam = AddExam(_course);

        await Assert.ThrowsAsync<ValidationException>(() => Mediator.Send(new RecordGradeCommand(exam.Id, Guid.NewGuid(), 87.555m, null)));
        await Assert.ThrowsAsync<ValidationException>(() => Mediator.Send(new RecordGradeCommand(exam.Id, Guid.NewGuid(), -1m, null)));
    }

    // ---- Payments and invoices ----

    [Fact]
    public async Task RecordPayment_WithAFutureDate_IsRejected()
    {
        var invoice = AddInvoice(AddStudent(_smouha), 500);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            Mediator.Send(new RecordPaymentCommand(invoice.Id, 100, Today.AddDays(5), PaymentMethod.Cash)));

        Assert.Contains("future", ex.Errors.Single());
        Assert.Empty(Context.Payments);
    }

    [Fact]
    public async Task CreateInvoice_FromAPackageAtAnotherBranch_IsRejected()
    {
        var kafrCourse = AddCourse(_kafrAbdo, name: "Robotics");
        var kafrPackage = AddPackage(kafrCourse);
        var smouhaStudent = AddStudent(_smouha);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            Mediator.Send(new CreateInvoiceCommand(smouhaStudent.Id, kafrPackage.Id, null, Today.AddDays(10))));

        Assert.Contains("different branch", ex.Errors.Single());
        Assert.Empty(Context.Invoices);
    }

    // ---- Payroll ----

    [Fact]
    public async Task HourlyPayroll_RoundsEachLineToTheCent_SoTheTotalEqualsTheSumOfItsLines()
    {
        var teacher = AddTeacher(_smouha, PayType.Hourly, 100);
        var room = AddRoom(_smouha);
        var monday = new DateTime(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 3; i++)
        {
            AddSession(_course, room, teacher, monday.AddDays(i), TimeSpan.FromMinutes(20));   // 100/h x 20 min = 33.333...
        }

        var run = await new GeneratePayrollRunCommandHandler(Context)
            .Handle(new GeneratePayrollRunCommand(teacher.Id, new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 31)), CancellationToken.None);

        Context.ChangeTracker.Clear();
        var lines = Context.PayrollLineItems.Where(l => l.PayrollRunId == run.Id).Select(l => l.Amount).ToList();
        Assert.All(lines, amount => Assert.Equal(33.33m, amount));
        Assert.Equal(lines.Sum(), run.TotalAmount);   // 99.99, not the unrounded 100.00 that the column would never hold
        Assert.Equal(99.99m, run.TotalAmount);
    }

    // ---- Student transfers ----

    [Fact]
    public async Task Transfer_WhileEnrolledInACourseAtTheCurrentBranch_IsRefused()
    {
        var student = AddStudent(_smouha);
        Enroll(student, _course);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            Mediator.Send(new TransferStudentBranchCommand(student.Id, _kafrAbdo.Id, "Moved house")));

        Assert.Contains("Drop them before transferring", ex.Errors.Single());
        Context.ChangeTracker.Clear();
        Assert.Equal(_smouha.Id, Context.Students.Single().CurrentBranchId);
        Assert.Empty(Context.StudentBranchHistories);
    }

    [Fact]
    public async Task Transfer_AfterDroppingTheEnrollments_Succeeds_AndRecordsTheHistory()
    {
        var student = AddStudent(_smouha);
        var enrollment = Enroll(student, _course);
        await Mediator.Send(new DropEnrollmentCommand(enrollment.Id));

        var moved = await Mediator.Send(new TransferStudentBranchCommand(student.Id, _kafrAbdo.Id, "Moved house"));

        Assert.Equal(_kafrAbdo.Id, moved.CurrentBranchId);
        Assert.Equal(_smouha.Id, Context.StudentBranchHistories.Single().FromBranchId);
    }
}

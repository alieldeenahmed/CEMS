using CEMS.Application.Common.Exceptions;
using CEMS.Application.Payments;
using CEMS.Application.Payments.Commands.CancelInvoice;
using CEMS.Application.Payments.Commands.CreateInvoice;
using CEMS.Application.Payments.Commands.RecordPayment;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Payments;
using CEMS.Domain.Students;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Payments;

public class InvoiceLifecycleTests : HandlerTestBase
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private Branch _branch = null!;
    private Student _student = null!;
    private Package _package = null!;

    private void Seed(string role = RoleNames.FrontDesk)
    {
        _branch = new Branch { Id = Guid.NewGuid(), Name = "Smouha" };
        var curriculum = new Curriculum { Id = Guid.NewGuid(), Name = "Web", Description = "Web" };
        var course = new Course { Id = Guid.NewGuid(), Name = "Python", DeliveryMode = DeliveryMode.Group, CurriculumId = curriculum.Id, BranchId = _branch.Id };
        _package = new Package { Id = Guid.NewGuid(), CourseId = course.Id, SessionCount = 12, Price = 2400 };
        _student = new Student
        {
            Id = Guid.NewGuid(), FullName = "Khaled Hany", DateOfBirth = new DateOnly(2013, 4, 12),
            Gender = Gender.Male, EnrollmentDate = new DateOnly(2024, 1, 1), CurrentBranchId = _branch.Id
        };

        Context.AddRange(_branch, curriculum, course, _package, _student);
        Context.SaveChanges();

        CurrentUser.UserId = Guid.NewGuid();
        CurrentUser.Roles = [role];
        CurrentUser.BranchIds = [_branch.Id];
    }

    private async Task<Guid> CreateInvoice(decimal amount)
    {
        var dto = await new CreateInvoiceCommandHandler(Context, CurrentUser)
            .Handle(new CreateInvoiceCommand(_student.Id, null, amount, Today.AddDays(14)), CancellationToken.None);
        return dto.Id;
    }

    private Task<PaymentDto> Pay(Guid invoiceId, decimal amount) =>
        new RecordPaymentCommandHandler(Context, CurrentUser)
            .Handle(new RecordPaymentCommand(invoiceId, amount, Today, PaymentMethod.Cash), CancellationToken.None);

    private InvoiceStatus StatusOf(Guid invoiceId) => Context.Invoices.Single(i => i.Id == invoiceId).Status;

    [Fact]
    public async Task CreateInvoice_FromPackage_TakesAmountFromPackagePrice()
    {
        Seed();

        var dto = await new CreateInvoiceCommandHandler(Context, CurrentUser)
            .Handle(new CreateInvoiceCommand(_student.Id, _package.Id, null, Today.AddDays(14)), CancellationToken.None);

        Assert.Equal(2400, dto.Amount);
        Assert.Equal(InvoiceStatus.Pending, dto.Status);
    }

    [Fact]
    public async Task CreateInvoice_PackageAmountWinsOverClientSuppliedAmount()
    {
        Seed();

        var dto = await new CreateInvoiceCommandHandler(Context, CurrentUser)
            .Handle(new CreateInvoiceCommand(_student.Id, _package.Id, 1, Today.AddDays(14)), CancellationToken.None);

        Assert.Equal(2400, dto.Amount);
    }

    [Fact]
    public async Task CreateInvoice_AdHocWithoutAmount_ThrowsBadRequest()
    {
        Seed();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            new CreateInvoiceCommandHandler(Context, CurrentUser)
                .Handle(new CreateInvoiceCommand(_student.Id, null, null, Today.AddDays(14)), CancellationToken.None));
    }

    [Fact]
    public async Task CreateInvoice_UserWithoutAccessToStudentsBranch_ThrowsForbidden()
    {
        Seed();
        CurrentUser.BranchIds = [Guid.NewGuid()];

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateInvoice(100));
    }

    [Fact]
    public async Task RecordPayment_PartialAmount_MovesInvoiceToPartiallyPaid()
    {
        Seed();
        var invoiceId = await CreateInvoice(1000);

        await Pay(invoiceId, 400);

        Assert.Equal(InvoiceStatus.PartiallyPaid, StatusOf(invoiceId));
    }

    [Fact]
    public async Task RecordPayment_MoreThanHalfButNotAll_StaysPartiallyPaid()
    {
        // Regression: the new payment used to be counted twice (once via EF relationship fix-up on
        // invoice.Payments, once from the request), so any payment over 50% marked the invoice Paid.
        Seed();
        var invoiceId = await CreateInvoice(1000);

        await Pay(invoiceId, 600);

        Assert.Equal(InvoiceStatus.PartiallyPaid, StatusOf(invoiceId));
    }

    [Fact]
    public async Task RecordPayment_ExactAmount_MovesInvoiceToPaid()
    {
        Seed();
        var invoiceId = await CreateInvoice(1000);

        await Pay(invoiceId, 1000);

        Assert.Equal(InvoiceStatus.Paid, StatusOf(invoiceId));
    }

    [Fact]
    public async Task RecordPayment_InstalmentsAreSummedAcrossPayments()
    {
        Seed();
        var invoiceId = await CreateInvoice(1000);

        await Pay(invoiceId, 400);
        Assert.Equal(InvoiceStatus.PartiallyPaid, StatusOf(invoiceId));

        await Pay(invoiceId, 599);
        Assert.Equal(InvoiceStatus.PartiallyPaid, StatusOf(invoiceId));

        await Pay(invoiceId, 1);
        Assert.Equal(InvoiceStatus.Paid, StatusOf(invoiceId));
    }

    [Fact]
    public async Task RecordPayment_MoreThanTheRemainingBalance_IsRejected_AndNothingIsRecorded()
    {
        Seed();
        var invoiceId = await CreateInvoice(1000);
        await Pay(invoiceId, 400);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Pay(invoiceId, 700));

        Assert.Contains("remaining balance of 600.00", ex.Errors.Single());
        Context.ChangeTracker.Clear();
        Assert.Equal(InvoiceStatus.PartiallyPaid, StatusOf(invoiceId));
        Assert.Single(Context.Payments);
    }

    [Fact]
    public async Task RecordPayment_ExactlyTheRemainingBalance_SettlesTheInvoice()
    {
        Seed();
        var invoiceId = await CreateInvoice(1000);
        await Pay(invoiceId, 400);

        await Pay(invoiceId, 600);

        Assert.Equal(InvoiceStatus.Paid, StatusOf(invoiceId));
    }

    [Fact]
    public async Task RecordPayment_OnAFullyPaidInvoice_IsRejected()
    {
        Seed();
        var invoiceId = await CreateInvoice(1000);
        await Pay(invoiceId, 1000);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Pay(invoiceId, 1));

        Assert.Contains("already fully paid", ex.Errors.Single());
    }

    [Fact]
    public async Task RecordPayment_AgainstCancelledInvoice_ThrowsBadRequest()
    {
        Seed(RoleNames.Owner);
        var invoiceId = await CreateInvoice(1000);
        await new CancelInvoiceCommandHandler(Context, CurrentUser)
            .Handle(new CancelInvoiceCommand(invoiceId), CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(() => Pay(invoiceId, 100));
    }

    [Fact]
    public async Task RecordPayment_UserWithoutAccessToStudentsBranch_ThrowsForbidden()
    {
        Seed();
        var invoiceId = await CreateInvoice(1000);
        CurrentUser.BranchIds = [Guid.NewGuid()];

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Pay(invoiceId, 100));
    }

    [Fact]
    public async Task CancelInvoice_PartiallyPaid_IsAllowed()
    {
        Seed(RoleNames.Owner);
        var invoiceId = await CreateInvoice(1000);
        await Pay(invoiceId, 100);

        await new CancelInvoiceCommandHandler(Context, CurrentUser)
            .Handle(new CancelInvoiceCommand(invoiceId), CancellationToken.None);

        Assert.Equal(InvoiceStatus.Cancelled, StatusOf(invoiceId));
    }

    [Fact]
    public async Task CancelInvoice_FullyPaid_ThrowsBadRequest()
    {
        Seed(RoleNames.Owner);
        var invoiceId = await CreateInvoice(1000);
        await Pay(invoiceId, 1000);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            new CancelInvoiceCommandHandler(Context, CurrentUser)
                .Handle(new CancelInvoiceCommand(invoiceId), CancellationToken.None));
    }

    [Fact]
    public async Task CancelInvoice_AlreadyCancelled_ThrowsBadRequest()
    {
        Seed(RoleNames.Owner);
        var invoiceId = await CreateInvoice(1000);
        var handler = new CancelInvoiceCommandHandler(Context, CurrentUser);
        await handler.Handle(new CancelInvoiceCommand(invoiceId), CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new CancelInvoiceCommand(invoiceId), CancellationToken.None));
    }

    [Fact]
    public void InvoiceDto_PastDueUnpaidInvoice_IsOverdueAndPaidOneIsNot()
    {
        Seed();
        var overdue = new Invoice
        {
            Id = Guid.NewGuid(), StudentId = _student.Id, Amount = 500, IssuedDate = Today.AddDays(-30),
            DueDate = Today.AddDays(-1), Status = InvoiceStatus.Pending
        };
        var paid = new Invoice
        {
            Id = Guid.NewGuid(), StudentId = _student.Id, Amount = 500, IssuedDate = Today.AddDays(-30),
            DueDate = Today.AddDays(-1), Status = InvoiceStatus.Paid
        };

        Assert.True(InvoiceDto.FromEntity(overdue, Today).IsOverdue);
        Assert.False(InvoiceDto.FromEntity(paid, Today).IsOverdue);
    }
}

using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Exams;
using CEMS.Domain.Payments;
using CEMS.Domain.Students;
using CEMS.Domain.Teachers;

namespace CEMS.Application.Tests.TestSupport;

/// <summary>
/// Small builders for the entities almost every handler test needs, so each test class can describe
/// only what's specific to it. Every Add* method saves immediately and returns the tracked entity.
/// </summary>
public abstract class SeededHandlerTestBase : HandlerTestBase
{
    /// <summary>Acts as a user with the given role, scoped to the given branches.</summary>
    protected void ActAs(string role, params Branch[] branches)
    {
        CurrentUser.UserId = Guid.NewGuid();
        CurrentUser.Roles = [role];
        CurrentUser.BranchIds = branches.Select(b => b.Id).ToList();
    }

    protected Branch AddBranch(string name = "Smouha")
    {
        var branch = new Branch { Id = Guid.NewGuid(), Name = name, Address = "1 Test St", Phone = "0300000000" };
        Context.Branches.Add(branch);
        Context.SaveChanges();
        return branch;
    }

    protected Room AddRoom(Branch branch, int capacity = 15, string name = "Lab 1")
    {
        var room = new Room { Id = Guid.NewGuid(), Name = name, Capacity = capacity, BranchId = branch.Id };
        Context.Rooms.Add(room);
        Context.SaveChanges();
        return room;
    }

    protected Curriculum AddCurriculum(string name = "Web & Software")
    {
        var curriculum = new Curriculum { Id = Guid.NewGuid(), Name = name, Description = name };
        Context.Curricula.Add(curriculum);
        Context.SaveChanges();
        return curriculum;
    }

    protected Course AddCourse(Branch branch, Curriculum? curriculum = null, string name = "Python Fundamentals")
    {
        curriculum ??= AddCurriculum();
        var course = new Course
        {
            Id = Guid.NewGuid(), Name = name, DeliveryMode = DeliveryMode.Group,
            CurriculumId = curriculum.Id, BranchId = branch.Id
        };
        Context.Courses.Add(course);
        Context.SaveChanges();
        return course;
    }

    protected Teacher AddTeacher(Branch? branch = null, PayType payType = PayType.Hourly, decimal payRate = 100)
    {
        var teacher = new Teacher
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), HireDate = new DateOnly(2024, 1, 1),
            PayType = payType, PayRate = payRate
        };
        Context.Teachers.Add(teacher);

        if (branch is not null)
        {
            Context.TeacherBranches.Add(new TeacherBranch { TeacherId = teacher.Id, BranchId = branch.Id });
        }

        Context.SaveChanges();
        return teacher;
    }

    protected Student AddStudent(Branch branch, string name = "Khaled Hany")
    {
        var student = new Student
        {
            Id = Guid.NewGuid(), FullName = name, DateOfBirth = new DateOnly(2013, 4, 12), Gender = Gender.Male,
            EnrollmentDate = new DateOnly(2024, 1, 1), CurrentBranchId = branch.Id
        };
        Context.Students.Add(student);
        Context.SaveChanges();
        return student;
    }

    protected Guardian AddGuardian(string name = "Hany Mahmoud")
    {
        var guardian = new Guardian { Id = Guid.NewGuid(), FullName = name, Phone = "01100000000", Email = "guardian@example.com" };
        Context.Guardians.Add(guardian);
        Context.SaveChanges();
        return guardian;
    }

    protected CourseEnrollment Enroll(Student student, Course course, CourseEnrollmentStatus status = CourseEnrollmentStatus.Active)
    {
        var enrollment = new CourseEnrollment
        {
            Id = Guid.NewGuid(), StudentId = student.Id, CourseId = course.Id,
            EnrollmentDate = new DateOnly(2024, 1, 1), Status = status
        };
        Context.CourseEnrollments.Add(enrollment);
        Context.SaveChanges();
        return enrollment;
    }

    protected CourseSession AddSession(Course course, Room room, Teacher teacher, DateTime start, TimeSpan? length = null, SessionStatus status = SessionStatus.Scheduled)
    {
        var session = new CourseSession
        {
            Id = Guid.NewGuid(), CourseId = course.Id, RoomId = room.Id, TeacherId = teacher.Id,
            StartUtc = start, EndUtc = start + (length ?? TimeSpan.FromHours(1)), Status = status
        };
        Context.CourseSessions.Add(session);
        Context.SaveChanges();
        return session;
    }

    protected Exam AddExam(Course course, decimal maxScore = 100, string name = "Midterm")
    {
        var exam = new Exam { Id = Guid.NewGuid(), CourseId = course.Id, Name = name, MaxScore = maxScore, ExamDate = new DateOnly(2025, 1, 1) };
        Context.Exams.Add(exam);
        Context.SaveChanges();
        return exam;
    }

    protected Package AddPackage(Course course, decimal price = 2400, int sessions = 12)
    {
        var package = new Package { Id = Guid.NewGuid(), CourseId = course.Id, SessionCount = sessions, Price = price };
        Context.Packages.Add(package);
        Context.SaveChanges();
        return package;
    }

    protected Invoice AddInvoice(Student student, decimal amount, InvoiceStatus status = InvoiceStatus.Pending, DateOnly? due = null)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), StudentId = student.Id, Amount = amount, Status = status,
            IssuedDate = new DateOnly(2025, 1, 1), DueDate = due ?? new DateOnly(2030, 1, 1)
        };
        Context.Invoices.Add(invoice);
        Context.SaveChanges();
        return invoice;
    }

    protected Payment AddPayment(Invoice invoice, decimal amount, DateOnly? date = null)
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(), InvoiceId = invoice.Id, AmountPaid = amount,
            PaymentDate = date ?? new DateOnly(2025, 1, 2), Method = PaymentMethod.Cash, ReceivedByUserId = Guid.NewGuid()
        };
        Context.Payments.Add(payment);
        Context.SaveChanges();
        return payment;
    }
}

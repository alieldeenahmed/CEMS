using CEMS.Application.Common.Exceptions;
using CEMS.Application.Exams;
using CEMS.Application.Exams.Commands.CreateExam;
using CEMS.Application.Exams.Commands.RecordGrade;
using CEMS.Application.Exams.Commands.UpdateExam;
using CEMS.Application.Exams.Queries.GenerateReportCard;
using CEMS.Application.Exams.Queries.GetExamById;
using CEMS.Application.Exams.Queries.GetExamsForCourse;
using CEMS.Application.Exams.Queries.GetGradesForExam;
using CEMS.Application.Exams.Queries.GetGradesForStudent;
using CEMS.Application.Tests.TestSupport;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Exams;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;

namespace CEMS.Application.Tests.Exams;

public class ExamHandlerTests : SeededHandlerTestBase
{
    private class CapturingReportCardGenerator : IReportCardGenerator
    {
        public ReportCardData? Captured { get; private set; }

        public byte[] Generate(ReportCardData data)
        {
            Captured = data;
            return [1, 2, 3];
        }
    }

    private readonly Branch _smouha;
    private readonly Branch _kafrAbdo;
    private readonly Course _course;
    private readonly Teacher _teacher;

    public ExamHandlerTests()
    {
        _smouha = AddBranch("Smouha");
        _kafrAbdo = AddBranch("Kafr Abdo");
        _course = AddCourse(_smouha, name: "Python");
        _teacher = AddTeacher(_smouha);
        AddSession(_course, AddRoom(_smouha), _teacher, new DateTime(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc));
    }

    private void ActAsTeacher(Teacher teacher)
    {
        CurrentUser.UserId = teacher.UserId;
        CurrentUser.Roles = [RoleNames.Teacher];
        CurrentUser.BranchIds = [];
    }

    private static CreateExamCommand NewExam(Guid courseId) => new(courseId, "Midterm", 100, new DateOnly(2030, 2, 1));

    // ---- Exam management: who may do it ----

    [Fact]
    public async Task CreateExam_OwnerBranchManagerAndTheCoursesTeacher_AreAllowed()
    {
        ActAs(RoleNames.Owner);
        await new CreateExamCommandHandler(Context, CurrentUser).Handle(NewExam(_course.Id), CancellationToken.None);

        ActAs(RoleNames.BranchManager, _smouha);
        await new CreateExamCommandHandler(Context, CurrentUser).Handle(NewExam(_course.Id), CancellationToken.None);

        ActAsTeacher(_teacher);
        await new CreateExamCommandHandler(Context, CurrentUser).Handle(NewExam(_course.Id), CancellationToken.None);

        Assert.Equal(3, Context.Exams.Count());
    }

    [Fact]
    public async Task CreateExam_FrontDesk_ATeacherOfAnotherCourse_AndAnotherBranchsManager_AreForbidden()
    {
        var otherTeacher = AddTeacher(_smouha);
        var handler = () => new CreateExamCommandHandler(Context, CurrentUser);

        ActAs(RoleNames.FrontDesk, _smouha);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler().Handle(NewExam(_course.Id), CancellationToken.None));

        ActAsTeacher(otherTeacher);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler().Handle(NewExam(_course.Id), CancellationToken.None));

        ActAs(RoleNames.BranchManager, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler().Handle(NewExam(_course.Id), CancellationToken.None));

        Assert.Empty(Context.Exams);
    }

    [Fact]
    public async Task CreateExam_UnknownCourse_ThrowsNotFound()
    {
        ActAs(RoleNames.Owner);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CreateExamCommandHandler(Context, CurrentUser).Handle(NewExam(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateExam_ChangesIt_ButOnlyForThoseWhoManageTheCourse()
    {
        var exam = AddExam(_course);

        ActAs(RoleNames.FrontDesk, _smouha);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new UpdateExamCommandHandler(Context, CurrentUser).Handle(new UpdateExamCommand(exam.Id, "Hacked", 1, new DateOnly(2030, 1, 1)), CancellationToken.None));

        ActAsTeacher(_teacher);
        var dto = await new UpdateExamCommandHandler(Context, CurrentUser)
            .Handle(new UpdateExamCommand(exam.Id, "Final", 50, new DateOnly(2030, 5, 5)), CancellationToken.None);

        Assert.Equal(("Final", 50m), (dto.Name, dto.MaxScore));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateExamCommandHandler(Context, CurrentUser).Handle(new UpdateExamCommand(Guid.NewGuid(), "x", 1, new DateOnly(2030, 1, 1)), CancellationToken.None));
    }

    // ---- Recording grades ----

    private Task<GradeDto> Grade(Exam exam, Domain.Students.Student student, decimal score, string? comment = null) =>
        new RecordGradeCommandHandler(Context, CurrentUser).Handle(new RecordGradeCommand(exam.Id, student.Id, score, comment), CancellationToken.None);

    [Fact]
    public async Task RecordGrade_ForAnEnrolledStudent_StoresTheScoreWithWhoGradedItAndWhen()
    {
        var exam = AddExam(_course);
        var student = AddStudent(_smouha);
        Enroll(student, _course);
        ActAsTeacher(_teacher);

        var dto = await Grade(exam, student, 88, "Great work");

        Assert.Equal(88, dto.Score);
        Assert.Equal(_teacher.UserId, dto.GradedByUserId);
        Assert.NotNull(dto.GradedAtUtc);
        Assert.Equal(88, Context.Grades.Single().Score);
    }

    [Fact]
    public async Task RecordGrade_Twice_UpdatesTheSameGradeInsteadOfAddingASecond()
    {
        var exam = AddExam(_course);
        var student = AddStudent(_smouha);
        Enroll(student, _course);
        ActAs(RoleNames.BranchManager, _smouha);

        await Grade(exam, student, 60);
        await Grade(exam, student, 75, "Retake");

        var stored = Assert.Single(Context.Grades);
        Assert.Equal(75, stored.Score);
        Assert.Equal("Retake", stored.Comments);
    }

    [Fact]
    public async Task RecordGrade_AboveTheMaximumScore_ThrowsBadRequest_ButTheMaximumItselfIsFine()
    {
        var exam = AddExam(_course, maxScore: 50);
        var student = AddStudent(_smouha);
        Enroll(student, _course);
        ActAs(RoleNames.Owner);

        await Grade(exam, student, 50);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Grade(exam, student, 50.5m));

        Assert.Contains("50", ex.Errors[0]);
    }

    [Fact]
    public async Task RecordGrade_ForAStudentWhoIsNotActivelyEnrolled_ThrowsBadRequest()
    {
        var exam = AddExam(_course);
        var outsider = AddStudent(_smouha, "Outsider");
        var dropped = AddStudent(_smouha, "Dropped");
        Enroll(dropped, _course, CourseEnrollmentStatus.Dropped);
        var waitlisted = AddStudent(_smouha, "Waitlisted");
        Enroll(waitlisted, _course, CourseEnrollmentStatus.Waitlisted);
        ActAs(RoleNames.Owner);

        await Assert.ThrowsAsync<BadRequestException>(() => Grade(exam, outsider, 50));
        await Assert.ThrowsAsync<BadRequestException>(() => Grade(exam, dropped, 50));
        await Assert.ThrowsAsync<BadRequestException>(() => Grade(exam, waitlisted, 50));
        Assert.Empty(Context.Grades);
    }

    [Fact]
    public async Task RecordGrade_ByATeacherWhoDoesNotTeachTheCourse_OrByFrontDesk_IsForbidden()
    {
        var exam = AddExam(_course);
        var student = AddStudent(_smouha);
        Enroll(student, _course);

        ActAsTeacher(AddTeacher(_smouha));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Grade(exam, student, 70));

        ActAs(RoleNames.FrontDesk, _smouha);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Grade(exam, student, 70));
        Assert.Empty(Context.Grades);
    }

    [Fact]
    public async Task RecordGrade_UnknownExam_ThrowsNotFound()
    {
        ActAs(RoleNames.Owner);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new RecordGradeCommandHandler(Context, CurrentUser).Handle(new RecordGradeCommand(Guid.NewGuid(), Guid.NewGuid(), 1, null), CancellationToken.None));
    }

    // ---- Reads ----

    [Fact]
    public async Task GradesForExam_ListsEveryActiveStudent_UngradedOnesWithANullScore_SortedByName()
    {
        var exam = AddExam(_course);
        var graded = AddStudent(_smouha, "Zaki");
        var ungraded = AddStudent(_smouha, "Amira");
        var dropped = AddStudent(_smouha, "Dropped");
        Enroll(graded, _course);
        Enroll(ungraded, _course);
        Enroll(dropped, _course, CourseEnrollmentStatus.Dropped);
        Context.Grades.Add(new Grade { Id = Guid.NewGuid(), ExamId = exam.Id, StudentId = graded.Id, Score = 91 });
        Context.SaveChanges();
        ActAsTeacher(_teacher);

        var result = await new GetGradesForExamQueryHandler(Context, CurrentUser).Handle(new GetGradesForExamQuery(exam.Id), CancellationToken.None);

        Assert.Equal(["Amira", "Zaki"], result.Select(r => r.StudentFullName).ToArray());
        Assert.Null(result[0].Score);
        Assert.Equal(91, result[1].Score);
    }

    [Fact]
    public async Task ExamReads_AreAvailableToBranchStaffAndTheCoursesTeacher_ButNotAnotherBranch()
    {
        var exam = AddExam(_course);

        ActAs(RoleNames.FrontDesk, _smouha);
        Assert.Single(await new GetExamsForCourseQueryHandler(Context, CurrentUser).Handle(new GetExamsForCourseQuery(_course.Id), CancellationToken.None));
        Assert.Equal(exam.Id, (await new GetExamByIdQueryHandler(Context, CurrentUser).Handle(new GetExamByIdQuery(exam.Id), CancellationToken.None)).Id);

        ActAsTeacher(_teacher);
        Assert.Single(await new GetExamsForCourseQueryHandler(Context, CurrentUser).Handle(new GetExamsForCourseQuery(_course.Id), CancellationToken.None));

        ActAs(RoleNames.BranchManager, _kafrAbdo);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetExamsForCourseQueryHandler(Context, CurrentUser).Handle(new GetExamsForCourseQuery(_course.Id), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetExamByIdQueryHandler(Context, CurrentUser).Handle(new GetExamByIdQuery(exam.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetExamByIdQueryHandler(Context, CurrentUser).Handle(new GetExamByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task ExamsForCourse_AreOrderedByDate()
    {
        var later = AddExam(_course, name: "Later");
        later.ExamDate = new DateOnly(2030, 6, 1);
        var earlier = AddExam(_course, name: "Earlier");
        earlier.ExamDate = new DateOnly(2030, 3, 1);
        Context.SaveChanges();
        ActAs(RoleNames.Owner);

        var result = await new GetExamsForCourseQueryHandler(Context, CurrentUser).Handle(new GetExamsForCourseQuery(_course.Id), CancellationToken.None);

        Assert.Equal(["Earlier", "Later"], result.Select(e => e.Name).ToArray());
    }

    [Fact]
    public async Task GradesForStudent_StaffSeeEveryGrade_ATeacherOnlyThoseFromCoursesTheyTeach()
    {
        var student = AddStudent(_smouha);
        var otherCourse = AddCourse(_smouha, name: "Scratch");
        Enroll(student, _course);
        Enroll(student, otherCourse);
        var mine = AddExam(_course, name: "Python Midterm");
        var others = AddExam(otherCourse, name: "Scratch Midterm");
        Context.Grades.AddRange(
            new Grade { Id = Guid.NewGuid(), ExamId = mine.Id, StudentId = student.Id, Score = 80 },
            new Grade { Id = Guid.NewGuid(), ExamId = others.Id, StudentId = student.Id, Score = 90 });
        Context.SaveChanges();

        ActAs(RoleNames.FrontDesk, _smouha);
        var staffView = await new GetGradesForStudentQueryHandler(Context, CurrentUser).Handle(new GetGradesForStudentQuery(student.Id), CancellationToken.None);

        ActAsTeacher(_teacher);
        var teacherView = await new GetGradesForStudentQueryHandler(Context, CurrentUser).Handle(new GetGradesForStudentQuery(student.Id), CancellationToken.None);

        Assert.Equal(2, staffView.Count);
        Assert.Equal("Python Midterm", Assert.Single(teacherView).ExamName);
    }

    [Fact]
    public async Task GradesForStudent_ATeacherWhoDoesNotTeachThemIsForbidden()
    {
        var student = AddStudent(_smouha);
        ActAsTeacher(AddTeacher(_smouha));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetGradesForStudentQueryHandler(Context, CurrentUser).Handle(new GetGradesForStudentQuery(student.Id), CancellationToken.None));
    }

    // ---- Report card ----

    [Fact]
    public async Task ReportCard_GroupsExamsByCourse_WithNullScoresForUngradedExams()
    {
        var student = AddStudent(_smouha, "Khaled Hany");
        var scratch = AddCourse(_smouha, name: "Scratch");
        Enroll(student, _course);
        Enroll(student, scratch);
        var graded = AddExam(_course, name: "Python Midterm");
        AddExam(scratch, name: "Scratch Final");
        Context.Grades.Add(new Grade { Id = Guid.NewGuid(), ExamId = graded.Id, StudentId = student.Id, Score = 88 });
        Context.SaveChanges();
        ActAs(RoleNames.FrontDesk, _smouha);
        var generator = new CapturingReportCardGenerator();

        var bytes = await new GenerateReportCardQueryHandler(Context, CurrentUser, generator)
            .Handle(new GenerateReportCardQuery(student.Id), CancellationToken.None);

        Assert.Equal([1, 2, 3], bytes);
        Assert.Equal("Khaled Hany", generator.Captured!.StudentName);
        Assert.Equal(["Python", "Scratch"], generator.Captured.Courses.Select(c => c.CourseName).OrderBy(n => n).ToArray());
        var python = generator.Captured.Courses.Single(c => c.CourseName == "Python");
        Assert.Equal(88, Assert.Single(python.Exams).Score);
        Assert.Null(Assert.Single(generator.Captured.Courses.Single(c => c.CourseName == "Scratch").Exams).Score);
    }

    [Fact]
    public async Task ReportCard_ForAStudentAtAnotherBranch_IsForbidden_AndDoesNotRenderAnything()
    {
        var student = AddStudent(_kafrAbdo);
        ActAs(RoleNames.FrontDesk, _smouha);
        var generator = new CapturingReportCardGenerator();

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GenerateReportCardQueryHandler(Context, CurrentUser, generator).Handle(new GenerateReportCardQuery(student.Id), CancellationToken.None));

        Assert.Null(generator.Captured);
    }

    [Fact]
    public void RecordGradeValidator_RejectsNegativeScores()
    {
        var validator = new RecordGradeCommandValidator();

        Assert.True(validator.Validate(new RecordGradeCommand(Guid.NewGuid(), Guid.NewGuid(), 0, null)).IsValid);
        Assert.False(validator.Validate(new RecordGradeCommand(Guid.NewGuid(), Guid.NewGuid(), -1, null)).IsValid);
    }
}

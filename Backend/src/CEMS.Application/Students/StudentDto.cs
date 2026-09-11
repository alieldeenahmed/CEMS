using CEMS.Domain.Students;

namespace CEMS.Application.Students;

public record StudentDto(
    Guid Id,
    string FullName,
    DateOnly DateOfBirth,
    Gender Gender,
    DateOnly EnrollmentDate,
    StudentStatus Status,
    Guid CurrentBranchId)
{
    public static StudentDto FromEntity(Student student) =>
        new(student.Id, student.FullName, student.DateOfBirth, student.Gender, student.EnrollmentDate, student.Status, student.CurrentBranchId);
}

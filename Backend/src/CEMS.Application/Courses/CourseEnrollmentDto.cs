using CEMS.Domain.Courses;

namespace CEMS.Application.Courses;

public record CourseEnrollmentDto(Guid Id, Guid StudentId, Guid CourseId, string CourseName, DateOnly EnrollmentDate, CourseEnrollmentStatus Status, int? Position);

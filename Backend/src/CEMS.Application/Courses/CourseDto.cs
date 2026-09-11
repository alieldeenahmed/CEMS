using CEMS.Domain.Courses;

namespace CEMS.Application.Courses;

public record CourseDto(Guid Id, string Name, DeliveryMode DeliveryMode, Guid SubjectId, Guid BranchId);

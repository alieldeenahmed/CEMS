namespace CEMS.Application.Payments;

public record PackageDto(Guid Id, Guid CourseId, int SessionCount, decimal Price);

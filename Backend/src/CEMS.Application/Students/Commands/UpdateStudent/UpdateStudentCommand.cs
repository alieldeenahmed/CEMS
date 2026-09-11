using CEMS.Domain.Students;
using MediatR;

namespace CEMS.Application.Students.Commands.UpdateStudent;

public record UpdateStudentCommand(Guid Id, string FullName, DateOnly DateOfBirth, Gender Gender, StudentStatus Status) : IRequest<StudentDto>;

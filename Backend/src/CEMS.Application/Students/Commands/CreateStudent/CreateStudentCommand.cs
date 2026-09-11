using CEMS.Domain.Students;
using MediatR;

namespace CEMS.Application.Students.Commands.CreateStudent;

public record CreateStudentCommand(string FullName, DateOnly DateOfBirth, Gender Gender, Guid BranchId) : IRequest<StudentDto>;

using MediatR;

namespace CEMS.Application.Exams.Commands.DeleteExam;

public record DeleteExamCommand(Guid Id) : IRequest;

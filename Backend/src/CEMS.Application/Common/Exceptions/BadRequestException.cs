namespace CEMS.Application.Common.Exceptions;

public class BadRequestException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public BadRequestException(IEnumerable<string> errors) : base("One or more errors occurred.")
    {
        Errors = errors.ToList();
    }
}

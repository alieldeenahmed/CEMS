namespace CEMS.Application.Common.Exceptions;

public class SchedulingConflictException : Exception
{
    public IReadOnlyList<string> Conflicts { get; }

    public SchedulingConflictException(IEnumerable<string> conflicts, Exception? innerException = null)
        : base("A scheduling conflict was detected.", innerException)
    {
        Conflicts = conflicts.ToList();
    }
}

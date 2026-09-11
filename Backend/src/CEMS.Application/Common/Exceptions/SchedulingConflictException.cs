namespace CEMS.Application.Common.Exceptions;

public class SchedulingConflictException : Exception
{
    public IReadOnlyList<string> Conflicts { get; }

    public SchedulingConflictException(IEnumerable<string> conflicts) : base("A scheduling conflict was detected.")
    {
        Conflicts = conflicts.ToList();
    }
}

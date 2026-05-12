using UniScheduler.Application.Common.Models;

namespace UniScheduler.Application.Common.Exceptions;

public class ConflictException : Exception
{
    public IReadOnlyList<ConflictInfo> Conflicts { get; }

    public ConflictException(IReadOnlyList<ConflictInfo> conflicts)
        : base("Schedule conflict detected.")
    {
        Conflicts = conflicts;
    }
}

namespace UniScheduler.Domain.Entities;

public class PairTimeSlot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UniversityId { get; set; }
    public int PairNumber { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public University University { get; set; } = null!;
}

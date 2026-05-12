namespace UniScheduler.Domain.Entities;

public class PairTimeSlot
{
    public int PairNumber { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

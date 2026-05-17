using UniScheduler.Domain.Common;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Domain.Entities;

public class Room : Entity
{
    public Guid BuildingId { get; set; }
    public string Number { get; set; } = string.Empty;
    public RoomType RoomType { get; set; }
    public int Capacity { get; set; }
    public bool HasProjector { get; set; }
    public bool HasComputers { get; set; }
    public bool HasLab { get; set; }
    public bool IsOnline { get; set; }
    /// <summary>Floor number (1-based). Used for intra-building travel time calculation.</summary>
    public int Floor { get; set; } = 1;
    /// <summary>Metres from the main staircase/lift to this room's door.</summary>
    public int DistanceFromStairsMeters { get; set; } = 0;
    /// <summary>Lesson types the scheduler may place here. Empty list = all types allowed.</summary>
    public List<LessonType> AllowedLessonTypes { get; set; } = new();

    public Building Building { get; set; } = null!;
    public ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();
}

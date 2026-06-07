using UniScheduler.Domain.Common;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Domain.Entities;

// Hard binding: when a row exists for a (Subject, LessonType), that one may only be placed in
// one of the rooms. No rows = no restriction, one row per allowed room.
public class SubjectRoomBinding : Entity
{
    public Guid SubjectId { get; set; }
    public LessonType LessonType { get; set; }
    public Guid RoomId { get; set; }

    public Subject Subject { get; set; } = null!;
    public Room Room { get; set; } = null!;
}

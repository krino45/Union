using UniScheduler.Domain.Enums;

namespace UniScheduler.Domain.Entities;

public class TeacherSubject
{
    public Guid TeacherId { get; set; }
    public Guid SubjectId { get; set; }
    public LessonType LessonType { get; set; }
    public RoomType? PreferredRoomType { get; set; }

    public Teacher Teacher { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
}

using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Common.Models;

public static class RoomLessonCompatibility
{
    public static bool RoomTypePermits(LessonType lessonType, RoomType roomType) => (lessonType, roomType) switch
    {
        (LessonType.Lecture, RoomType.LectureHall) => true,
        (LessonType.Lecture, RoomType.RegularCabinet) => true,
        (LessonType.Practical, RoomType.RegularCabinet) => true,
        (LessonType.Practical, RoomType.ComputerLab) => true,
        (LessonType.Seminar, RoomType.RegularCabinet) => true,
        (LessonType.Lab, RoomType.Lab) => true,
        (LessonType.Lab, RoomType.ComputerLab) => true,
        _ => false
    };
}

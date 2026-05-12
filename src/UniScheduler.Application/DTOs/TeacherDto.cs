using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.DTOs;

public record TeacherDto(
    Guid Id,
    string FirstName,
    string LastName,
    string MiddleName,
    string DisplayName,
    string Email,
    List<TeacherSubjectDto> Subjects
);

public record TeacherSubjectDto(Guid SubjectId, string SubjectName, LessonType LessonType, RoomType? PreferredRoomType);

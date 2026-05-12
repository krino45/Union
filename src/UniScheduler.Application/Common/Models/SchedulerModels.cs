using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Common.Models;

//  Input 

public record SchedulerInput(
    Guid ScheduleId,
    IReadOnlyList<SchedulerRoom> Rooms,
    IReadOnlyList<SchedulerTeacher> Teachers,
    IReadOnlyList<SchedulerGroup> Groups,
    IReadOnlyList<SchedulerRequirement> Requirements,
    IReadOnlyList<SchedulerBuildingDistance> BuildingDistances,
    IReadOnlyList<SchedulerBlock> TeacherBlocks,
    int DaysPerWeek = 6,
    int PairsPerDay = 6,
    int SolverTimeoutSeconds = 60
);

public record SchedulerRoom(
    Guid Id,
    Guid BuildingId,
    RoomType RoomType,
    int Capacity,
    bool HasProjector,
    bool HasComputers,
    bool HasLab,
    bool IsOnline,
    int Floor = 1,
    int DistanceFromStairsMeters = 0,
    int StairsDistancePerFloor = 20
);

public record SchedulerTeacher(Guid Id);

public record SchedulerGroup(Guid Id, int StudentCount);

/// <summary>
/// One class-per-week occurrence that must be placed in the schedule.
/// Merged lectures (multiple groups) carry all GroupIds.
/// </summary>
public record SchedulerRequirement(
    int Index,
    IReadOnlyList<Guid> GroupIds,
    Guid SubjectId,
    LessonType LessonType,
    Guid TeacherId,
    WeekType WeekType,
    bool IsOnline,
    bool NeedsProjector,
    bool NeedsComputers,
    bool NeedsLab
);

public record SchedulerBuildingDistance(Guid FromId, Guid ToId, int DistanceMeters);

public record SchedulerBlock(Guid TeacherId, RussianDayOfWeek Day, int PairNumber, WeekType WeekType);

//  Output 

public record SchedulerOutput(
    SolverStatus Status,
    string? Message,
    IReadOnlyList<SchedulerAssignment> Assignments
);

public enum SolverStatus { Optimal, Feasible, Infeasible, Unknown }

public record SchedulerAssignment(
    int RequirementIndex,
    RussianDayOfWeek Day,
    int PairNumber,
    WeekType WeekType,
    Guid RoomId
);

//  Conflict detection 

public record ConflictInfo(ConflictType Type, string Description);

public enum ConflictType
{
    RoomDoubleBooked,
    TeacherDoubleBooked,
    GroupDoubleBooked,
    TravelTimeExceeded
}

//  Excel import 

public class ImportPreviewDto
{
    public List<ImportEntryDto> ValidEntries { get; set; } = new();
    public List<ImportErrorDto> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool HasErrors => Errors.Any();
}

public record ImportEntryDto(
    string GroupName,
    string SubjectName,
    string TeacherName,
    string RoomNumber,
    RussianDayOfWeek DayOfWeek,
    int PairNumber,
    WeekType WeekType,
    LessonType LessonType
);

public record ImportErrorDto(int Row, int Col, string Message);

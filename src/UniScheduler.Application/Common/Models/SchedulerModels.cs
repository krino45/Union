using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Common.Models;

public record SolverWeights(
    int StudentWindow = 100,
    int TeacherWindow = 80,
    int ActiveDay = 60,
    int SanPinOverload = 300,
    int ConsecLecture = 70,
    int ConsecSeminar = 40,
    int ConsecPractical = 30,
    int ConsecLab = 10,
    int EarlyPair = 15,
    int MiddlePair = 0,
    int LatePair = 25,
    int ConsecRunScalar = 3,
    int SaturdayPenalty = 30,
    int DepartmentMismatchPenalty = 50,
    int WalkingPenaltyMax = 120,
    int StairFloorMeters = 20)
{
    #region  Constructors

    public SolverWeights() : this(100)
    {
    }

    public SolverWeights(SolverSettings? solverSettings) : this()
    {
        if (solverSettings is null)
            return;
        StudentWindow = solverSettings.StudentWindow;
        TeacherWindow = solverSettings.TeacherWindow;
        ActiveDay = solverSettings.ActiveDay;
        SanPinOverload = solverSettings.SanPinOverload;
        ConsecLecture = solverSettings.ConsecLecture;
        ConsecSeminar = solverSettings.ConsecSeminar;
        ConsecPractical = solverSettings.ConsecPractical;
        ConsecLab = solverSettings.ConsecLab;
        EarlyPair = solverSettings.EarlyPair;
        MiddlePair = solverSettings.MiddlePair;
        LatePair = solverSettings.LatePair;
        ConsecRunScalar = solverSettings.ConsecRunScalar;
        SaturdayPenalty = solverSettings.SaturdayPenalty;
        DepartmentMismatchPenalty = solverSettings.DepartmentMismatchPenalty;
        WalkingPenaltyMax = solverSettings.WalkingPenaltyMax;
        StairFloorMeters = solverSettings.StairFloorMeters;
    }

    public SolverSettings ToSolverSettings()
    {
        return new SolverSettings
        {
            StudentWindow = StudentWindow,
            TeacherWindow = TeacherWindow,
            ActiveDay = ActiveDay,
            SanPinOverload = SanPinOverload,
            ConsecLecture = ConsecLecture,
            ConsecSeminar = ConsecSeminar,
            ConsecPractical = ConsecPractical,
            ConsecLab = ConsecLab,
            EarlyPair = EarlyPair,
            MiddlePair = MiddlePair,
            LatePair = LatePair,
            ConsecRunScalar = ConsecRunScalar,
            SaturdayPenalty = SaturdayPenalty,
            DepartmentMismatchPenalty = DepartmentMismatchPenalty,
            WalkingPenaltyMax = WalkingPenaltyMax,
            StairFloorMeters = StairFloorMeters
        };
    }

    #endregion
};

public record SchedulerInput(
    Guid ScheduleId,
    IReadOnlyList<SchedulerRoom> Rooms,
    IReadOnlyList<SchedulerTeacher> Teachers,
    IReadOnlyList<SchedulerGroup> Groups,
    IReadOnlyList<SchedulerRequirement> Requirements,
    IReadOnlyList<SchedulerBuildingDistance> BuildingDistances,
    IReadOnlyList<SchedulerBlock> TeacherBlocks,
    int DaysPerWeek = 6,
    int PairsPerDay = 7,
    IReadOnlyList<int>? BreakMinutesBetweenPairs = null,
    int SolverTimeoutSeconds = 60,
    IReadOnlyList<SchedulerRoomDistance>? RoomDistances = null,
    SolverWeights? Weights = null,
    IReadOnlyList<SchedulerZoneEntryDistance>? ZoneEntryDistances = null,
    // Cells the scheduler must treat as already taken
    IReadOnlyList<SchedulerRoomBlock>? RoomBlocks = null,
    // LNS repair mode: each pin forces its requirement onto the given (day, pair, week, room).
    // Pinned reqs emit a single BoolVar (the matching cell); reqs not in this list are free.
    IReadOnlyList<SchedulerPin>? Pinnings = null,
    // LNS warm-start: advisory placements for freed reqs. The solver calls AddHint(v, 1) for
    // the matching BoolVar — no constraint, just a search heuristic that gives CP-SAT a cheap
    // initial upper bound. Hints with no matching candidate cell are silently ignored.
    IReadOnlyList<SchedulerHint>? Hints = null,
    // True for LNS repair solves (pins + small free core)
    bool IsRepairSolve = false
);

// LNS: hard-fix one requirement to a specific placement. WeekType must equal the requirement's
// own WeekType (Both/Odd/Even). Validation in the solver rejects pins that conflict with room
// compatibility, teacher availability blocks, room blocks, or group day blocks.
public record SchedulerPin(
    int RequirementIndex,
    RussianDayOfWeek Day,
    int PairNumber,
    WeekType WeekType,
    Guid RoomId
);

// LNS: advisory "try this first" placement. Same shape as a pin but never constraining —
// solver may ignore. Used to warm-start kicks from the current incumbent placements of freed
// reqs so CP-SAT finds the existing solution quickly and prunes worse branches.
public record SchedulerHint(
    int RequirementIndex,
    RussianDayOfWeek Day,
    int PairNumber,
    WeekType WeekType,
    Guid RoomId
);

public record SchedulerZoneEntryDistance(Guid BuildingId, int Floor, int EntryDistanceMeters);

public record SchedulerRoomBlock(Guid RoomId, RussianDayOfWeek Day, int PairNumber, WeekType WeekType);

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
    IReadOnlyList<LessonType>? AllowedLessonTypes = null,
    Guid? DepartmentFacultyId = null,
    bool IsDistributed = false,
    int EntryDistanceMeters = 0
);

public record SchedulerRoomDistance(Guid FromRoomId, Guid ToRoomId, int DistanceMeters);

public record SchedulerTeacher(Guid Id);

public record SchedulerGroup(Guid Id, int StudentCount, IReadOnlyList<int>? BlockedDays = null);

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
    bool NeedsLab,
    Guid? SubjectFacultyId = null,
    // Requirements sharing a non-null ParallelKey are parallel sessions of one logical class
    // (language streams / lab subgroups): co-scheduled to the same slot, exempt from mutual
    // group conflict, and (for languages) placed in the distributed sentinel room.
    int? ParallelKey = null,
    string? SubgroupLabel = null,
    int? HeadcountOverride = null,
    bool RequiresDistributedRoom = false,
    bool RequiresSportsHall = false
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

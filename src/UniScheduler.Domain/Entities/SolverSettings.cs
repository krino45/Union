using UniScheduler.Domain.Common;

namespace UniScheduler.Domain.Entities;

public class SolverSettings : Entity
{
    public Guid UniversityId { get; set; }

    // S1 / S2 / S3 / S5
    public int StudentWindow  { get; set; } = 100;
    public int TeacherWindow  { get; set; } = 80;
    public int ActiveDay      { get; set; } = 60;
    public int SanPinOverload { get; set; } = 300;

    // S6 - consecutive same lesson
    public int ConsecLecture  { get; set; } = 70;
    public int ConsecSeminar  { get; set; } = 40;
    public int ConsecPractical { get; set; } = 30;
    public int ConsecLab      { get; set; } = 10;

    // S7 - pair time preference
    public int EarlyPair  { get; set; } = 15;
    public int MiddlePair { get; set; } = 0;
    public int LatePair   { get; set; } = 25;

    // S6 run-length scalar
    public int ConsecRunScalar { get; set; } = 3;

    // S8 - Saturday discouragement
    public int SaturdayPenalty { get; set; } = 30;

    // S9 - room department mismatch
    public int DepartmentMismatchPenalty { get; set; } = 50;

    // S4 - walking penalty max
    public int WalkingPenaltyMax { get; set; } = 120;

    // S4 - fixed walking-distance equivalent (metres) per floor climbed via stairs/elevators
    public int StairFloorMeters { get; set; } = 20;

    // SanPiN - max physical-education pair-slots per group per day (1 or 2; varies by university)
    public int MaxPePerDay { get; set; } = 1;

    // SanPiN - penalty when PE is not the day's last lesson
    public int PeNotLastPenalty { get; set; } = 40;

    // Reward (applied as a negative S6 weight) for placing two same-day PE pairs consecutively
    public int PeConsecutiveReward { get; set; } = 30;

    public University University { get; set; } = null!;
}

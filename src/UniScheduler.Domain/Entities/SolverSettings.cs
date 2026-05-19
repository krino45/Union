namespace UniScheduler.Domain.Entities;

public class SolverSettings
{
    public Guid Id { get; set; } = Guid.Empty;

    // S1 / S2 / S3 / S5
    public int StudentWindow  { get; set; } = 100;
    public int TeacherWindow  { get; set; } = 80;
    public int ActiveDay      { get; set; } = 60;
    public int SanPinOverload { get; set; } = 300;

    // S6 — consecutive same lesson
    public int ConsecLecture  { get; set; } = 70;
    public int ConsecSeminar  { get; set; } = 40;
    public int ConsecPractical { get; set; } = 30;
    public int ConsecLab      { get; set; } = 10;

    // S7 — pair time preference
    public int EarlyPair  { get; set; } = 15;  // penalty per step below pair 3
    public int MiddlePair { get; set; } = 0;   // flat penalty for pairs 3–4
    public int LatePair   { get; set; } = 25;  // penalty per step above pair 4

    // S6 run-length scalar — extra multiplier for runs of 3+ consecutive same-type lessons
    public int ConsecRunScalar { get; set; } = 3;

    // S8 — Saturday discouragement (soft penalty per group-slot on Saturday)
    public int SaturdayPenalty { get; set; } = 30;

    // S9 — room department / subject department faculty mismatch
    public int DepartmentMismatchPenalty { get; set; } = 50;

    // S4 — walking penalty max coefficient (penalty when walkMins / breakMins ≈ 1.0)
    public int WalkingPenaltyMax { get; set; } = 120;
}

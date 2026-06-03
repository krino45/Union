namespace UniScheduler.Application.Common.Config;

public static class SchedulerEnv
{
    // OR-Tools CP-SAT parameters forwarded into solver.StringParameters.
    public const string SolverNumWorkers = "SOLVER_NUM_WORKERS";
    public const string SolverLinearizationLevel = "SOLVER_LINEARIZATION_LEVEL";
    public const string SolverProbingLevel = "SOLVER_PROBING_LEVEL";

    // Generator batching: target group count per per-batch CP-SAT solve (default 5).
    public const string BatchGroupTarget = "UNISCHEDULER_BATCH_GROUP_TARGET";

    // LNS polish phase budgets.
    public const string LnsBudgetMin = "UNISCHEDULER_LNS_BUDGET_MIN"; // total wall-clock minutes (default 5)
    public const string LnsKickSec = "UNISCHEDULER_LNS_KICK_SEC";   // per-kick CP-SAT seconds (default 10)

    // Foreign-language requirement-builder: students-per-teacher cap. ⌈groupSize / cap⌉ teachers
    // are emitted per group's language slot. Default 15.
    public const string LangPerTeacherCap = "UNISCHEDULER_LANG_PER_TEACHER_CAP";

    // Physical-education requirement-builder: students-per-teacher cap. ⌈groupSize / cap⌉ teachers
    // are emitted per group's PE slot. Default 40.
    public const string PePerTeacherCap = "UNISCHEDULER_PE_PER_TEACHER_CAP";
}

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
    public const string LnsWorkers = "UNISCHEDULER_LNS_WORKERS";
    // Late-acceptance history length L for the LNS hill-climb (default 20).
    public const string LnsLahcHistory = "UNISCHEDULER_LNS_LAHC";
    // Per-kick destroy sizing: Target = reqs freed by RandomK/WorstK (default 80); Min = WorstK floor
    // (default 20). Bigger = wider neighborhood per kick, slower kicks. Cheap now travel is out of repair.
    public const string LnsDestroyTarget = "UNISCHEDULER_LNS_DESTROY_TARGET";
    public const string LnsDestroyMin = "UNISCHEDULER_LNS_DESTROY_MIN";

    // Foreign-language requirement-builder: students-per-teacher cap. ⌈groupSize / cap⌉ teachers
    // are emitted per group's language slot. Default 15.
    public const string LangPerTeacherCap = "UNISCHEDULER_LANG_PER_TEACHER_CAP";

    // Physical-education requirement-builder: students-per-teacher cap. ⌈groupSize / cap⌉ teachers
    // are emitted per group's PE slot. Default 40.
    public const string PePerTeacherCap = "UNISCHEDULER_PE_PER_TEACHER_CAP";
}

using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Schedules;

internal static class ScheduleAccessGuard
{
    // Returns true if the current user can see + work with this schedule.
    // - Published / Archived: any authenticated admin in the same university (already filtered upstream).
    // - Draft: owner, open-to-admins drafts, SuperAdmin, or owner-less legacy drafts.
    public static bool CanAccess(Schedule schedule, ICurrentUserService user)
    {
        if (user.IsSuperAdmin) return true;
        if (schedule.Status != ScheduleStatus.Draft) return true;
        if (schedule.OwnerUserId == null) return true;
        if (schedule.IsOpenToAdmins) return true;
        return schedule.OwnerUserId == user.UserId;
    }

    public static void EnsureCanEdit(Schedule schedule, ICurrentUserService user)
    {
        if (!CanAccess(schedule, user))
            throw new ForbiddenException("Этот черновик расписания закрыт для редактирования.");
    }

    public static void EnsureOwnerOnly(Schedule schedule, ICurrentUserService user, string action)
    {
        if (user.IsSuperAdmin) return;
        if (schedule.OwnerUserId == null) return; // legacy
        if (schedule.OwnerUserId != user.UserId)
            throw new ForbiddenException($"Только владелец черновика может {action}.");
    }

    // Called when an edit demotes a Published schedule back to Draft.
    // The editor takes ownership so they can republish; access is reset to private.
    public static void TransferOwnershipOnDemote(Schedule schedule, ICurrentUserService user)
    {
        if (user.UserId == null) return;
        schedule.OwnerUserId = user.UserId;
        schedule.IsOpenToAdmins = false;
    }
}

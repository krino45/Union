using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.Features.Invitations.Commands;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Auth.Queries;

// Returns sanitized invitation info so the register page can decide between
// "registration", "accept" or "wrong account" UX without leaking details.
public record InvitationInfoDto(
    bool IsValid,                 // false: expired/consumed/not-found
    string? UniversityName,
    string? UniversityShortName,
    UniversityRole? UniversityRole,
    string? TeacherDisplayName,   // null if not teacher-linked
    bool TeacherAlreadyLinked,    // true: some AppUser already has TeacherId == invitation.TeacherId
    string Mode);                 // "register" | "accept" | "wrong-account" | "invalid"

public record GetInvitationInfoQuery(string Token) : IRequest<InvitationInfoDto>;

public class GetInvitationInfoQueryHandler : IRequestHandler<GetInvitationInfoQuery, InvitationInfoDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    public GetInvitationInfoQueryHandler(IApplicationDbContext db, ICurrentUserService user)
    { _db = db; _user = user; }

    public async Task<InvitationInfoDto> Handle(GetInvitationInfoQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return new InvitationInfoDto(false, null, null, null, null, false, "invalid");

        var otpHash = CreateInvitationCommandHandler.HashToken(request.Token);
        var inv = await _db.Invitations
            .Include(i => i.University)
            .Include(i => i.Teacher)
            .FirstOrDefaultAsync(i => i.OtpHash == otpHash, cancellationToken);

        if (inv == null || inv.ConsumedAt != null || inv.ExpiresAt < DateTime.UtcNow)
            return new InvitationInfoDto(false, null, null, null, null, false, "invalid");

        var teacherDisplay = inv.Teacher != null
            ? $"{inv.Teacher.LastName} {inv.Teacher.FirstName}".Trim()
            : null;

        bool teacherAlreadyLinked = false;
        if (inv.TeacherId.HasValue)
            teacherAlreadyLinked = await _db.AppUsers
                .AnyAsync(u => u.TeacherId == inv.TeacherId, cancellationToken);

        string mode;
        if (_user.UserId.HasValue)
        {
            // Logged-in flow: can the current user accept?
            var me = await _db.AppUsers
                .FirstOrDefaultAsync(u => u.Id == _user.UserId, cancellationToken);
            if (me == null)
                mode = "wrong-account";
            else if (inv.TeacherId.HasValue && me.TeacherId == inv.TeacherId)
                mode = "accept";
            else
                mode = "wrong-account";
        }
        else
        {
            // Anonymous flow: register, unless the teacher is already linked elsewhere
            mode = teacherAlreadyLinked ? "wrong-account" : "register";
        }

        return new InvitationInfoDto(
            true,
            inv.University.Name,
            inv.University.ShortName,
            inv.UniversityRole,
            teacherDisplay,
            teacherAlreadyLinked,
            mode);
    }
}

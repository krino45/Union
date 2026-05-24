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
    string? Email,                // the invited e-mail (so UI can prefill / explain a mismatch)
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
            return new InvitationInfoDto(false, null, null, null, null, null, false, "invalid");

        var otpHash = CreateInvitationCommandHandler.HashToken(request.Token);
        var inv = await _db.Invitations
            .Include(i => i.University)
            .Include(i => i.Teacher)
            .FirstOrDefaultAsync(i => i.OtpHash == otpHash, cancellationToken);

        if (inv == null || inv.ConsumedAt != null || inv.ExpiresAt < DateTime.UtcNow)
            return new InvitationInfoDto(false, null, null, null, null, null, false, "invalid");

        var teacherDisplay = inv.Teacher != null
            ? $"{inv.Teacher.LastName} {inv.Teacher.FirstName}".Trim()
            : null;

        // The "teacher already linked" flag here means: somebody other than the current caller is linked.
        var currentUserId = _user.UserId;
        bool teacherAlreadyLinkedElsewhere = false;
        if (inv.TeacherId.HasValue)
        {
            teacherAlreadyLinkedElsewhere = await _db.AppUsers
                .AnyAsync(u => u.TeacherId == inv.TeacherId && (!currentUserId.HasValue || u.Id != currentUserId.Value), cancellationToken);
        }

        var inviteEmail = inv.Email.Trim().ToLowerInvariant();
        string mode;
        if (currentUserId.HasValue)
        {
            var me = await _db.AppUsers
                .FirstOrDefaultAsync(u => u.Id == currentUserId.Value, cancellationToken);
            var myEmail = me?.Email?.Trim().ToLowerInvariant();
            // Identity gate: the logged-in session must own the invited e-mail.
            mode = (myEmail != null && myEmail == inviteEmail) ? "accept" : "wrong-account";
        }
        else
        {
            // Anonymous: offer the register page (which also exposes a "log in with existing account" tab).
            mode = "register";
        }

        return new InvitationInfoDto(
            true,
            inv.Email,
            inv.University.Name,
            inv.University.ShortName,
            inv.UniversityRole,
            teacherDisplay,
            teacherAlreadyLinkedElsewhere,
            mode);
    }
}

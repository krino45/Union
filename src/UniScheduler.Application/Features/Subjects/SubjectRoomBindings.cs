using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Subjects;

public record SubjectRoomBindingDto(LessonType LessonType, List<Guid> RoomIds);

// All room bindings for one subject, grouped by lesson type.
public record GetSubjectRoomBindingsQuery(Guid SubjectId) : IRequest<List<SubjectRoomBindingDto>>;

public class GetSubjectRoomBindingsQueryHandler
    : IRequestHandler<GetSubjectRoomBindingsQuery, List<SubjectRoomBindingDto>>
{
    private readonly IApplicationDbContext _db;
    public GetSubjectRoomBindingsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<SubjectRoomBindingDto>> Handle(GetSubjectRoomBindingsQuery request, CancellationToken ct)
    {
        var rows = await _db.SubjectRoomBindings
            .Where(b => b.SubjectId == request.SubjectId)
            .ToListAsync(ct);
        return rows
            .GroupBy(b => b.LessonType)
            .Select(g => new SubjectRoomBindingDto(g.Key, g.Select(b => b.RoomId).ToList()))
            .ToList();
    }
}

public record UpdateSubjectRoomBindingCommand(Guid SubjectId, LessonType LessonType, List<Guid> RoomIds) : IRequest;

public class UpdateSubjectRoomBindingCommandHandler : IRequestHandler<UpdateSubjectRoomBindingCommand>
{
    private readonly IApplicationDbContext _db;
    public UpdateSubjectRoomBindingCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(UpdateSubjectRoomBindingCommand request, CancellationToken ct)
    {
        var existing = await _db.SubjectRoomBindings
            .Where(b => b.SubjectId == request.SubjectId && b.LessonType == request.LessonType)
            .ToListAsync(ct);
        _db.SubjectRoomBindings.RemoveRange(existing);

        foreach (var roomId in (request.RoomIds ?? new List<Guid>()).Distinct())
        {
            _db.SubjectRoomBindings.Add(new SubjectRoomBinding
            {
                SubjectId = request.SubjectId,
                LessonType = request.LessonType,
                RoomId = roomId
            });
        }
        await _db.SaveChangesAsync(ct);
    }
}

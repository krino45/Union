using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;
using DomainAvailability = UniScheduler.Domain.Entities.TeacherAvailability;

namespace UniScheduler.Application.Features.TeacherAvailability.Commands;

public record CreateTeacherAvailabilityCommand(
    Guid TeacherId, RussianDayOfWeek DayOfWeek, int PairNumber, WeekType WeekType,
    string? Reason, bool IsRecurring, DateOnly? ValidFrom, DateOnly? ValidTo) : IRequest<TeacherAvailabilityDto>;

public record UpdateTeacherAvailabilityCommand(
    Guid Id, RussianDayOfWeek DayOfWeek, int PairNumber, WeekType WeekType,
    string? Reason, bool IsRecurring, DateOnly? ValidFrom, DateOnly? ValidTo) : IRequest<TeacherAvailabilityDto>;

public record DeleteTeacherAvailabilityCommand(Guid Id) : IRequest;

public class CreateTeacherAvailabilityCommandHandler : IRequestHandler<CreateTeacherAvailabilityCommand, TeacherAvailabilityDto>
{
    private readonly IApplicationDbContext _db;
    public CreateTeacherAvailabilityCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<TeacherAvailabilityDto> Handle(CreateTeacherAvailabilityCommand r, CancellationToken ct)
    {
        var teacher = await _db.Teachers.FindAsync(new object[] { r.TeacherId }, ct)
            ?? throw new NotFoundException(nameof(Teacher), r.TeacherId);

        var a = new DomainAvailability
        {
            TeacherId = r.TeacherId, DayOfWeek = r.DayOfWeek, PairNumber = r.PairNumber,
            WeekType = r.WeekType, Reason = r.Reason, IsRecurring = r.IsRecurring,
            ValidFrom = r.ValidFrom, ValidTo = r.ValidTo
        };
        _db.TeacherAvailabilities.Add(a);
        await _db.SaveChangesAsync(ct);
        return new TeacherAvailabilityDto(a.Id, a.TeacherId, teacher.LastName + " " + teacher.FirstName, a.DayOfWeek, a.PairNumber, a.WeekType, a.Reason, a.IsRecurring, a.ValidFrom, a.ValidTo);
    }
}

public class UpdateTeacherAvailabilityCommandHandler : IRequestHandler<UpdateTeacherAvailabilityCommand, TeacherAvailabilityDto>
{
    private readonly IApplicationDbContext _db;
    public UpdateTeacherAvailabilityCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<TeacherAvailabilityDto> Handle(UpdateTeacherAvailabilityCommand r, CancellationToken ct)
    {
        var a = await _db.TeacherAvailabilities.Include(x => x.Teacher).FirstOrDefaultAsync(x => x.Id == r.Id, ct)
            ?? throw new NotFoundException(nameof(DomainAvailability), r.Id);
        a.DayOfWeek = r.DayOfWeek; a.PairNumber = r.PairNumber; a.WeekType = r.WeekType;
        a.Reason = r.Reason; a.IsRecurring = r.IsRecurring; a.ValidFrom = r.ValidFrom; a.ValidTo = r.ValidTo;
        await _db.SaveChangesAsync(ct);
        return new TeacherAvailabilityDto(a.Id, a.TeacherId, a.Teacher.LastName + " " + a.Teacher.FirstName, a.DayOfWeek, a.PairNumber, a.WeekType, a.Reason, a.IsRecurring, a.ValidFrom, a.ValidTo);
    }
}

public class DeleteTeacherAvailabilityCommandHandler : IRequestHandler<DeleteTeacherAvailabilityCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteTeacherAvailabilityCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteTeacherAvailabilityCommand request, CancellationToken ct)
    {
        var a = await _db.TeacherAvailabilities.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(DomainAvailability), request.Id);
        _db.TeacherAvailabilities.Remove(a);
        await _db.SaveChangesAsync(ct);
    }
}

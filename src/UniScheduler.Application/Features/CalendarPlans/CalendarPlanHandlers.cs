using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.CalendarPlans;

//  Queries 

public record GetCalendarPlansQuery(int? AcademicYear = null, Term? Term = null)
    : IRequest<List<CalendarPlanDto>>;

public class GetCalendarPlansQueryHandler : IRequestHandler<GetCalendarPlansQuery, List<CalendarPlanDto>>
{
    private readonly IApplicationDbContext _db;
    public GetCalendarPlansQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<CalendarPlanDto>> Handle(GetCalendarPlansQuery req, CancellationToken ct)
    {
        var q = _db.CalendarPlans.Include(cp => cp.Weeks).AsQueryable();
        if (req.AcademicYear.HasValue) q = q.Where(cp => cp.AcademicYear == req.AcademicYear);
        if (req.Term.HasValue)        q = q.Where(cp => cp.Term == req.Term);
        return (await q.OrderBy(cp => cp.AcademicYear).ThenBy(cp => cp.Name).ToListAsync(ct))
               .Select(CalendarPlanQ.Map).ToList();
    }
}

public record GetCalendarPlanQuery(Guid Id) : IRequest<CalendarPlanDto>;

public class GetCalendarPlanQueryHandler : IRequestHandler<GetCalendarPlanQuery, CalendarPlanDto>
{
    private readonly IApplicationDbContext _db;
    public GetCalendarPlanQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CalendarPlanDto> Handle(GetCalendarPlanQuery req, CancellationToken ct)
    {
        var cp = await _db.CalendarPlans.Include(c => c.Weeks)
            .FirstOrDefaultAsync(c => c.Id == req.Id, ct)
            ?? throw new NotFoundException(nameof(CalendarPlan), req.Id);
        return CalendarPlanQ.Map(cp);
    }
}

//  Commands 

public record CreateCalendarPlanCommand(UpsertCalendarPlanDto Dto) : IRequest<CalendarPlanDto>;

public class CreateCalendarPlanCommandHandler : IRequestHandler<CreateCalendarPlanCommand, CalendarPlanDto>
{
    private readonly IApplicationDbContext _db;
    public CreateCalendarPlanCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CalendarPlanDto> Handle(CreateCalendarPlanCommand req, CancellationToken ct)
    {
        var cp = new CalendarPlan { Name = req.Dto.Name, AcademicYear = req.Dto.AcademicYear, Term = req.Dto.Term };
        _db.CalendarPlans.Add(cp);
        foreach (var w in req.Dto.Weeks)
            _db.CalendarWeeks.Add(new CalendarWeek { CalendarPlan = cp, StartDate = w.StartDate, Kind = w.Kind, Note = w.Note });
        await _db.SaveChangesAsync(ct);
        return CalendarPlanQ.Map((await _db.CalendarPlans.Include(c => c.Weeks).FirstOrDefaultAsync(c => c.Id == cp.Id, ct))!);
    }
}

public record UpdateCalendarPlanCommand(Guid Id, UpsertCalendarPlanDto Dto) : IRequest<CalendarPlanDto>;

public class UpdateCalendarPlanCommandHandler : IRequestHandler<UpdateCalendarPlanCommand, CalendarPlanDto>
{
    private readonly IApplicationDbContext _db;
    public UpdateCalendarPlanCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CalendarPlanDto> Handle(UpdateCalendarPlanCommand req, CancellationToken ct)
    {
        var cp = await _db.CalendarPlans.Include(c => c.Weeks)
            .FirstOrDefaultAsync(c => c.Id == req.Id, ct)
            ?? throw new NotFoundException(nameof(CalendarPlan), req.Id);

        cp.Name = req.Dto.Name; cp.AcademicYear = req.Dto.AcademicYear; cp.Term = req.Dto.Term;
        _db.CalendarWeeks.RemoveRange(cp.Weeks);
        foreach (var w in req.Dto.Weeks)
            _db.CalendarWeeks.Add(new CalendarWeek { CalendarPlanId = cp.Id, StartDate = w.StartDate, Kind = w.Kind, Note = w.Note });

        await _db.SaveChangesAsync(ct);
        return CalendarPlanQ.Map((await _db.CalendarPlans.Include(c => c.Weeks).FirstOrDefaultAsync(c => c.Id == cp.Id, ct))!);
    }
}

public record DeleteCalendarPlanCommand(Guid Id) : IRequest;

public class DeleteCalendarPlanCommandHandler : IRequestHandler<DeleteCalendarPlanCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteCalendarPlanCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteCalendarPlanCommand req, CancellationToken ct)
    {
        var cp = await _db.CalendarPlans.FindAsync(new object[] { req.Id }, ct)
            ?? throw new NotFoundException(nameof(CalendarPlan), req.Id);
        _db.CalendarPlans.Remove(cp);
        await _db.SaveChangesAsync(ct);
    }
}

//  Mapping 

public static class CalendarPlanQ
{
    public static CalendarPlanDto Map(CalendarPlan cp) => new(
        cp.Id, cp.Name, cp.AcademicYear, cp.Term,
        cp.Weeks.OrderBy(w => w.StartDate)
                .Select(w => new CalendarWeekDto(w.Id, w.StartDate, w.Kind, w.Note))
                .ToList()
    );
}

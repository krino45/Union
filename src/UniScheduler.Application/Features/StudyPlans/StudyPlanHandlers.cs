using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.StudyPlans;

//  Queries 

public record GetStudyPlansQuery(int? AcademicYear = null, Term? Term = null)
    : IRequest<List<StudyPlanDto>>;

public class GetStudyPlansQueryHandler : IRequestHandler<GetStudyPlansQuery, List<StudyPlanDto>>
{
    private readonly IApplicationDbContext _db;
    public GetStudyPlansQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<StudyPlanDto>> Handle(GetStudyPlansQuery req, CancellationToken ct)
    {
        var q = StudyPlanQ.BaseQuery(_db);
        if (req.AcademicYear.HasValue) q = q.Where(sp => sp.AcademicYear == req.AcademicYear);
        if (req.Term.HasValue)        q = q.Where(sp => sp.Term == req.Term);
        return (await q.OrderBy(sp => sp.AcademicYear).ThenBy(sp => sp.Name).ToListAsync(ct))
               .Select(StudyPlanQ.Map).ToList();
    }
}

public record GetStudyPlanQuery(Guid Id) : IRequest<StudyPlanDto>;

public class GetStudyPlanQueryHandler : IRequestHandler<GetStudyPlanQuery, StudyPlanDto>
{
    private readonly IApplicationDbContext _db;
    public GetStudyPlanQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<StudyPlanDto> Handle(GetStudyPlanQuery req, CancellationToken ct)
    {
        var plan = await StudyPlanQ.LoadById(_db, req.Id, ct)
            ?? throw new NotFoundException(nameof(StudyPlan), req.Id);
        return StudyPlanQ.Map(plan);
    }
}

//  Commands 

public record CreateStudyPlanCommand(UpsertStudyPlanDto Dto) : IRequest<StudyPlanDto>;

public class CreateStudyPlanCommandHandler : IRequestHandler<CreateStudyPlanCommand, StudyPlanDto>
{
    private readonly IApplicationDbContext _db;
    public CreateStudyPlanCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<StudyPlanDto> Handle(CreateStudyPlanCommand req, CancellationToken ct)
    {
        var plan = new StudyPlan
        {
            Name = req.Dto.Name, AcademicYear = req.Dto.AcademicYear,
            Term = req.Dto.Term, FacultyId = req.Dto.FacultyId,
            CalendarPlanId = req.Dto.CalendarPlanId
        };
        _db.StudyPlans.Add(plan);
        foreach (var id in req.Dto.GroupIds)
            _db.StudyPlanGroups.Add(new StudyPlanGroup { StudyPlan = plan, StudentGroupId = id });
        foreach (var e in req.Dto.Entries)
            _db.StudyPlanEntries.Add(StudyPlanQ.MakeEntry(plan.Id, e));
        await _db.SaveChangesAsync(ct);
        return StudyPlanQ.Map((await StudyPlanQ.LoadById(_db, plan.Id, ct))!);
    }
}

public record UpdateStudyPlanCommand(Guid Id, UpsertStudyPlanDto Dto) : IRequest<StudyPlanDto>;

public class UpdateStudyPlanCommandHandler : IRequestHandler<UpdateStudyPlanCommand, StudyPlanDto>
{
    private readonly IApplicationDbContext _db;
    public UpdateStudyPlanCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<StudyPlanDto> Handle(UpdateStudyPlanCommand req, CancellationToken ct)
    {
        var plan = await _db.StudyPlans
            .Include(sp => sp.Groups).Include(sp => sp.Entries)
            .FirstOrDefaultAsync(sp => sp.Id == req.Id, ct)
            ?? throw new NotFoundException(nameof(StudyPlan), req.Id);

        plan.Name = req.Dto.Name; plan.AcademicYear = req.Dto.AcademicYear;
        plan.Term = req.Dto.Term; plan.FacultyId = req.Dto.FacultyId;
        plan.CalendarPlanId = req.Dto.CalendarPlanId;

        _db.StudyPlanGroups.RemoveRange(plan.Groups);
        _db.StudyPlanEntries.RemoveRange(plan.Entries);
        foreach (var id in req.Dto.GroupIds)
            _db.StudyPlanGroups.Add(new StudyPlanGroup { StudyPlanId = plan.Id, StudentGroupId = id });
        foreach (var e in req.Dto.Entries)
            _db.StudyPlanEntries.Add(StudyPlanQ.MakeEntry(plan.Id, e));

        await _db.SaveChangesAsync(ct);
        return StudyPlanQ.Map((await StudyPlanQ.LoadById(_db, plan.Id, ct))!);
    }
}

public record DeleteStudyPlanCommand(Guid Id) : IRequest;

public class DeleteStudyPlanCommandHandler : IRequestHandler<DeleteStudyPlanCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteStudyPlanCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteStudyPlanCommand req, CancellationToken ct)
    {
        var plan = await _db.StudyPlans.FindAsync(new object[] { req.Id }, ct)
            ?? throw new NotFoundException(nameof(StudyPlan), req.Id);
        _db.StudyPlans.Remove(plan);
        await _db.SaveChangesAsync(ct);
    }
}

//  Shared helpers (internal — also used by audit query) 

public static class StudyPlanQ
{
    public static IQueryable<StudyPlan> BaseQuery(IApplicationDbContext db)
        => db.StudyPlans
             .Include(sp => sp.Faculty)
             .Include(sp => sp.CalendarPlan).ThenInclude(cp => cp!.Weeks)
             .Include(sp => sp.Groups).ThenInclude(g => g.StudentGroup)
             .Include(sp => sp.Entries).ThenInclude(e => e.Subject);

    public static Task<StudyPlan?> LoadById(IApplicationDbContext db, Guid id, CancellationToken ct)
        => BaseQuery(db).FirstOrDefaultAsync(sp => sp.Id == id, ct);

    public static StudyPlanDto Map(StudyPlan sp) => new(
        sp.Id, sp.Name, sp.AcademicYear, sp.Term,
        sp.FacultyId, sp.Faculty?.Name,
        sp.CalendarPlanId, sp.CalendarPlan?.Name,
        sp.Groups.Select(g => new StudyPlanGroupDto(g.StudentGroupId, g.StudentGroup.Name)).ToList(),
        sp.Entries.Select(e => new StudyPlanEntryDto(
            e.Id, e.SubjectId, e.Subject.Name, e.Subject.ShortName,
            e.LectureHours, e.PracticalHours, e.LabHours, e.SeminarHours, e.ThesisHours, e.LanguageHours
        )).OrderBy(e => e.SubjectName).ToList()
    );

    public static StudyPlanEntry MakeEntry(Guid planId, UpsertStudyPlanEntryDto e) => new()
    {
        StudyPlanId = planId, SubjectId = e.SubjectId,
        LectureHours = e.LectureHours, PracticalHours = e.PracticalHours,
        LabHours = e.LabHours, SeminarHours = e.SeminarHours, ThesisHours = e.ThesisHours,
        LanguageHours = e.LanguageHours
    };

    public static int StudyWeeksFromPlan(CalendarPlan? cp)
    {
        if (cp == null) return 18;
        var count = cp.Weeks.Count(w => w.Kind == Domain.Enums.WeekKind.Study);
        return count > 0 ? count : 18;
    }
}

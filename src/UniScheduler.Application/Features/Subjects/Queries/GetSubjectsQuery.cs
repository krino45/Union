using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Application.Features.Subjects.Queries;

public record GetSubjectsQuery(int? AcademicYear = null, Term? Term = null) : IRequest<List<SubjectDto>>;

public class GetSubjectsQueryHandler : IRequestHandler<GetSubjectsQuery, List<SubjectDto>>
{
    private readonly IApplicationDbContext db;

    public GetSubjectsQueryHandler(IApplicationDbContext db) => this.db = db;

    public async Task<List<SubjectDto>> Handle(GetSubjectsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Subjects.AsQueryable();
        if (request.AcademicYear.HasValue) query = query.Where(s => s.AcademicYear == request.AcademicYear);
        if (request.Term.HasValue) query = query.Where(s => s.Term == request.Term);
        return await query.OrderBy(s => s.AcademicYear).ThenBy(s => s.Term).ThenBy(s => s.Name)
            .Select(s => new SubjectDto(s.Id, s.Name, s.ShortName, s.AcademicYear, s.Term,
                s.LectureHoursPerWeek, s.PracticalHoursPerWeek, s.LabHoursPerWeek,
                s.LectureWeekType, s.PracticalWeekType, s.LabWeekType))
            .ToListAsync(cancellationToken);
    }
}

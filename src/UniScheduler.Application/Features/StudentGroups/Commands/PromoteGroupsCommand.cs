using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Interfaces;

namespace UniScheduler.Application.Features.StudentGroups.Commands;

public record PromoteGroupsCommand(Guid? FacultyId) : IRequest<int>;

public class PromoteGroupsCommandHandler : IRequestHandler<PromoteGroupsCommand, int>
{
    private readonly IApplicationDbContext db;

    public PromoteGroupsCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task<int> Handle(PromoteGroupsCommand request, CancellationToken cancellationToken)
    {
        var query = db.StudentGroups.AsQueryable();
        if (request.FacultyId.HasValue)
            query = query.Where(g => g.FacultyId == request.FacultyId.Value);

        var groups = await query.Where(g => g.Year < 6).ToListAsync(cancellationToken);
        foreach (var g in groups)
            g.Year++;

        await db.SaveChangesAsync(cancellationToken);
        return groups.Count;
    }
}

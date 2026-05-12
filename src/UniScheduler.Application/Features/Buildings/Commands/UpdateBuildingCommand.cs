using UniScheduler.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;

namespace UniScheduler.Application.Features.Buildings.Commands;

public record UpdateBuildingCommand(Guid Id, string ShortCode, string Address, int StairsDistancePerFloor) : IRequest<BuildingDto>;

public class UpdateBuildingCommandHandler : IRequestHandler<UpdateBuildingCommand, BuildingDto>
{
    private readonly IApplicationDbContext _db;
    public UpdateBuildingCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<BuildingDto> Handle(UpdateBuildingCommand request, CancellationToken cancellationToken)
    {
        var building = await _db.Buildings.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Building), request.Id);

        building.ShortCode = request.ShortCode;
        building.Address = request.Address;
        building.StairsDistancePerFloor = request.StairsDistancePerFloor;
        await _db.SaveChangesAsync(cancellationToken);
        return new BuildingDto(building.Id, building.ShortCode, building.Address, building.StairsDistancePerFloor);
    }
}

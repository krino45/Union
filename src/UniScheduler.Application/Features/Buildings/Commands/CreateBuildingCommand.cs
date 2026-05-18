using MediatR;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Buildings.Commands;

public record CreateBuildingCommand(string ShortCode, string Address, int NumberOfFloors = 5, int NumberOfBasementFloors = 0) : IRequest<BuildingDto>;

public class CreateBuildingCommandHandler : IRequestHandler<CreateBuildingCommand, BuildingDto>
{
    private readonly IApplicationDbContext _db;
    public CreateBuildingCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<BuildingDto> Handle(CreateBuildingCommand request, CancellationToken cancellationToken)
    {
        var building = new Building
        {
            ShortCode = request.ShortCode,
            Address = request.Address,
            NumberOfFloors = request.NumberOfFloors,
            NumberOfBasementFloors = request.NumberOfBasementFloors
        };
        _db.Buildings.Add(building);
        await _db.SaveChangesAsync(cancellationToken);
        return new BuildingDto(building.Id, building.ShortCode, building.Address, building.NumberOfFloors, building.NumberOfBasementFloors);
    }
}

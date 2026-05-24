using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations;

[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(UniScheduler.Infrastructure.Persistence.ApplicationDbContext))]
[Migration("20260519040000_AddWalkingPenaltyMaxToSolverSettings")]
public partial class AddWalkingPenaltyMaxToSolverSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "WalkingPenaltyMax",
            table: "SolverSettings",
            type: "integer",
            nullable: false,
            defaultValue: 120);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "WalkingPenaltyMax",
            table: "SolverSettings");
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations;

[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(UniScheduler.Infrastructure.Persistence.ApplicationDbContext))]
[Migration("20260519050000_AddMiddlePairToSolverSettings")]
public partial class AddMiddlePairToSolverSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "MiddlePair",
            table: "SolverSettings",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MiddlePair",
            table: "SolverSettings");
    }
}

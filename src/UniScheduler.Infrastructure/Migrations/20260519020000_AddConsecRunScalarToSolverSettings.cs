using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations;

[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(UniScheduler.Infrastructure.Persistence.ApplicationDbContext))]
[Migration("20260519020000_AddConsecRunScalarToSolverSettings")]
public partial class AddConsecRunScalarToSolverSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ConsecRunScalar",
            table: "SolverSettings",
            type: "integer",
            nullable: false,
            defaultValue: 3);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ConsecRunScalar",
            table: "SolverSettings");
    }
}

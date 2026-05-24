using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniScheduler.Infrastructure.Persistence;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260518000000_AddBaseScoreToSchedules")]
public partial class AddBaseScoreToSchedules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "BaseScore",
            table: "Schedules",
            type: "integer",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BaseScore",
            table: "Schedules");
    }
}

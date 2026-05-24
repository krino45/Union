using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniScheduler.Infrastructure.Persistence;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260517140000_AddThesisHoursToStudyPlanEntries")]
public partial class AddThesisHoursToStudyPlanEntries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "ThesisHours",
            table: "StudyPlanEntries",
            type: "double precision",
            nullable: false,
            defaultValue: 0.0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ThesisHours",
            table: "StudyPlanEntries");
    }
}

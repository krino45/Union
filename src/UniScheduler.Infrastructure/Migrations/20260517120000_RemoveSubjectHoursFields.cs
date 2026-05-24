using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniScheduler.Infrastructure.Persistence;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260517120000_RemoveSubjectHoursFields")]
    public partial class RemoveSubjectHoursFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LectureHoursPerWeek",  table: "Subjects");
            migrationBuilder.DropColumn(name: "PracticalHoursPerWeek", table: "Subjects");
            migrationBuilder.DropColumn(name: "LabHoursPerWeek",       table: "Subjects");
            migrationBuilder.DropColumn(name: "LectureWeekType",       table: "Subjects");
            migrationBuilder.DropColumn(name: "PracticalWeekType",     table: "Subjects");
            migrationBuilder.DropColumn(name: "LabWeekType",           table: "Subjects");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(name: "LectureHoursPerWeek",  table: "Subjects", nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<double>(name: "PracticalHoursPerWeek", table: "Subjects", nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<double>(name: "LabHoursPerWeek",       table: "Subjects", nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<int>(name: "LectureWeekType",   table: "Subjects", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "PracticalWeekType", table: "Subjects", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "LabWeekType",       table: "Subjects", nullable: false, defaultValue: 0);
        }
    }
}

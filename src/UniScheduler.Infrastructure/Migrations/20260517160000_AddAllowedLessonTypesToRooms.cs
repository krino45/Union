using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniScheduler.Infrastructure.Persistence;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260517160000_AddAllowedLessonTypesToRooms")]
public partial class AddAllowedLessonTypesToRooms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AllowedLessonTypes",
            table: "Rooms",
            type: "text",
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AllowedLessonTypes",
            table: "Rooms");
    }
}

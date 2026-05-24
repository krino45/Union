using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniScheduler.Infrastructure.Persistence;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260519000000_AddSolverSettings")]
public partial class AddSolverSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SolverSettings",
            columns: table => new
            {
                Id             = table.Column<Guid>(type: "uuid", nullable: false),
                StudentWindow  = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                TeacherWindow  = table.Column<int>(type: "integer", nullable: false, defaultValue: 80),
                ActiveDay      = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                SanPinOverload = table.Column<int>(type: "integer", nullable: false, defaultValue: 300),
                ConsecLecture  = table.Column<int>(type: "integer", nullable: false, defaultValue: 70),
                ConsecSeminar  = table.Column<int>(type: "integer", nullable: false, defaultValue: 40),
                ConsecPractical = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                ConsecLab      = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                EarlyPair      = table.Column<int>(type: "integer", nullable: false, defaultValue: 15),
                LatePair       = table.Column<int>(type: "integer", nullable: false, defaultValue: 25)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SolverSettings", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SolverSettings");
    }
}

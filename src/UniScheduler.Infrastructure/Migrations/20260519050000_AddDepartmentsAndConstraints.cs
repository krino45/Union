using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations;

[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(UniScheduler.Infrastructure.Persistence.ApplicationDbContext))]
[Migration("20260519050000_AddDepartmentsAndConstraints")]
public partial class AddDepartmentsAndConstraints : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Departments",
            columns: table => new
            {
                Id        = table.Column<Guid>(type: "uuid", nullable: false),
                Name      = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ShortCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                FacultyId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Departments", x => x.Id);
                table.ForeignKey(
                    name: "FK_Departments_Faculties_FacultyId",
                    column: x => x.FacultyId,
                    principalTable: "Faculties",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "GroupBlockedDays",
            columns: table => new
            {
                Id        = table.Column<Guid>(type: "uuid", nullable: false),
                GroupId   = table.Column<Guid>(type: "uuid", nullable: false),
                DayOfWeek = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GroupBlockedDays", x => x.Id);
                table.ForeignKey(
                    name: "FK_GroupBlockedDays_StudentGroups_GroupId",
                    column: x => x.GroupId,
                    principalTable: "StudentGroups",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.AddColumn<Guid>(
            name: "DepartmentId",
            table: "Rooms",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsEnabled",
            table: "Rooms",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<Guid>(
            name: "DepartmentId",
            table: "Subjects",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "SaturdayPenalty",
            table: "SolverSettings",
            type: "integer",
            nullable: false,
            defaultValue: 30);

        migrationBuilder.AddColumn<int>(
            name: "DepartmentMismatchPenalty",
            table: "SolverSettings",
            type: "integer",
            nullable: false,
            defaultValue: 50);

        migrationBuilder.CreateIndex(
            name: "IX_Departments_FacultyId",
            table: "Departments",
            column: "FacultyId");

        migrationBuilder.CreateIndex(
            name: "IX_GroupBlockedDays_GroupId_DayOfWeek",
            table: "GroupBlockedDays",
            columns: new[] { "GroupId", "DayOfWeek" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Rooms_DepartmentId",
            table: "Rooms",
            column: "DepartmentId");

        migrationBuilder.CreateIndex(
            name: "IX_Subjects_DepartmentId",
            table: "Subjects",
            column: "DepartmentId");

        migrationBuilder.AddForeignKey(
            name: "FK_Rooms_Departments_DepartmentId",
            table: "Rooms",
            column: "DepartmentId",
            principalTable: "Departments",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_Subjects_Departments_DepartmentId",
            table: "Subjects",
            column: "DepartmentId",
            principalTable: "Departments",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Rooms_Departments_DepartmentId", table: "Rooms");
        migrationBuilder.DropForeignKey(name: "FK_Subjects_Departments_DepartmentId", table: "Subjects");
        migrationBuilder.DropTable(name: "GroupBlockedDays");
        migrationBuilder.DropTable(name: "Departments");
        migrationBuilder.DropIndex(name: "IX_Rooms_DepartmentId", table: "Rooms");
        migrationBuilder.DropIndex(name: "IX_Subjects_DepartmentId", table: "Subjects");
        migrationBuilder.DropColumn(name: "DepartmentId", table: "Rooms");
        migrationBuilder.DropColumn(name: "IsEnabled", table: "Rooms");
        migrationBuilder.DropColumn(name: "DepartmentId", table: "Subjects");
        migrationBuilder.DropColumn(name: "SaturdayPenalty", table: "SolverSettings");
        migrationBuilder.DropColumn(name: "DepartmentMismatchPenalty", table: "SolverSettings");
    }
}

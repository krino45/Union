using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TenantIsolateSubjectsAndStudyPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UniversityId",
                table: "Subjects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UniversityId",
                table: "StudyPlans",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Backfill Subjects: resolve UniversityId via Department → Faculty chain
            migrationBuilder.Sql("""
                UPDATE "Subjects" s
                SET "UniversityId" = f."UniversityId"
                FROM "Departments" d
                JOIN "Faculties" f ON d."FacultyId" = f."Id"
                WHERE s."DepartmentId" = d."Id";
                """);
            // Subjects with no Department fall back to the first university in the table
            migrationBuilder.Sql("""
                UPDATE "Subjects"
                SET "UniversityId" = (SELECT "Id" FROM "Universities" ORDER BY "Id" LIMIT 1)
                WHERE "UniversityId" = '00000000-0000-0000-0000-000000000000';
                """);

            // Backfill StudyPlans: resolve UniversityId via Faculty
            migrationBuilder.Sql("""
                UPDATE "StudyPlans" sp
                SET "UniversityId" = f."UniversityId"
                FROM "Faculties" f
                WHERE sp."FacultyId" = f."Id";
                """);
            // StudyPlans with no Faculty fall back to the first university
            migrationBuilder.Sql("""
                UPDATE "StudyPlans"
                SET "UniversityId" = (SELECT "Id" FROM "Universities" ORDER BY "Id" LIMIT 1)
                WHERE "UniversityId" = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_UniversityId",
                table: "Subjects",
                column: "UniversityId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyPlans_UniversityId",
                table: "StudyPlans",
                column: "UniversityId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudyPlans_Universities_UniversityId",
                table: "StudyPlans",
                column: "UniversityId",
                principalTable: "Universities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Subjects_Universities_UniversityId",
                table: "Subjects",
                column: "UniversityId",
                principalTable: "Universities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudyPlans_Universities_UniversityId",
                table: "StudyPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_Subjects_Universities_UniversityId",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_Subjects_UniversityId",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_StudyPlans_UniversityId",
                table: "StudyPlans");

            migrationBuilder.DropColumn(
                name: "UniversityId",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "UniversityId",
                table: "StudyPlans");
        }
    }
}

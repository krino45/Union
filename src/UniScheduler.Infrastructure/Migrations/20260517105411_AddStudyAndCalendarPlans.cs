using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniScheduler.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyAndCalendarPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AcademicYear = table.Column<int>(type: "integer", nullable: false),
                    Term = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarWeeks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CalendarPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarWeeks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarWeeks_CalendarPlans_CalendarPlanId",
                        column: x => x.CalendarPlanId,
                        principalTable: "CalendarPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudyPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AcademicYear = table.Column<int>(type: "integer", nullable: false),
                    Term = table.Column<int>(type: "integer", nullable: false),
                    FacultyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CalendarPlanId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyPlans_CalendarPlans_CalendarPlanId",
                        column: x => x.CalendarPlanId,
                        principalTable: "CalendarPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StudyPlans_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StudyPlanEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudyPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    LectureHours = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    PracticalHours = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    LabHours = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    SeminarHours = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyPlanEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyPlanEntries_StudyPlans_StudyPlanId",
                        column: x => x.StudyPlanId,
                        principalTable: "StudyPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyPlanEntries_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudyPlanGroups",
                columns: table => new
                {
                    StudyPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentGroupId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyPlanGroups", x => new { x.StudyPlanId, x.StudentGroupId });
                    table.ForeignKey(
                        name: "FK_StudyPlanGroups_StudentGroups_StudentGroupId",
                        column: x => x.StudentGroupId,
                        principalTable: "StudentGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyPlanGroups_StudyPlans_StudyPlanId",
                        column: x => x.StudyPlanId,
                        principalTable: "StudyPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarWeeks_CalendarPlanId_StartDate",
                table: "CalendarWeeks",
                columns: new[] { "CalendarPlanId", "StartDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyPlanEntries_StudyPlanId_SubjectId",
                table: "StudyPlanEntries",
                columns: new[] { "StudyPlanId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyPlanEntries_SubjectId",
                table: "StudyPlanEntries",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyPlanGroups_StudentGroupId",
                table: "StudyPlanGroups",
                column: "StudentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyPlans_CalendarPlanId",
                table: "StudyPlans",
                column: "CalendarPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyPlans_FacultyId",
                table: "StudyPlans",
                column: "FacultyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarWeeks");

            migrationBuilder.DropTable(
                name: "StudyPlanEntries");

            migrationBuilder.DropTable(
                name: "StudyPlanGroups");

            migrationBuilder.DropTable(
                name: "StudyPlans");

            migrationBuilder.DropTable(
                name: "CalendarPlans");
        }
    }
}
